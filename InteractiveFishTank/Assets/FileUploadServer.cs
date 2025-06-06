using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class FileUploadServer : MonoBehaviour
{
    [Tooltip("Port number for the HTTP listener (e.g. 5000)")]
    public int listenPort = 5000;

    private string urlPrefix;
    private HttpListener listener;
    private Thread listenerThread;
    private string persistentPath; // cached Application.persistentDataPath

    // Queue for assets that need to be imported on the main thread
    private readonly ConcurrentQueue<string> importQueue = new ConcurrentQueue<string>();

    [SerializeField] private FishSpawner fishSpawner;

    void Awake()
    {
        // Cache persistentDataPath on the main thread
        persistentPath = Application.persistentDataPath;
    }

    void Start()
    {
        urlPrefix = $"http://*:{listenPort}/upload/";

        listener = new HttpListener();
        listener.Prefixes.Add(urlPrefix);

        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            Debug.LogError($"[FileUploadServer] Could not start HttpListener. Are you running Unity as Admin? Exception: {ex}");
            return;
        }

        listenerThread = new Thread(HandleIncomingConnections);
        listenerThread.IsBackground = true;
        listenerThread.Start();

        Debug.Log($"[FileUploadServer] Listening for uploads at {urlPrefix}");
    }

    void Update()
    {
#if UNITY_EDITOR
        // Process any pending imports on the main thread
        while (importQueue.TryDequeue(out string assetPath))
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[FileUploadServer] Asset imported at {assetPath}");
        }
#endif
    }

    void OnDestroy()
    {
        if (listener != null && listener.IsListening)
        {
            listener.Stop();
            listener.Close();
        }

        if (listenerThread != null && listenerThread.IsAlive)
            listenerThread.Abort();
    }

    private void HandleIncomingConnections()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context = null;
            try
            {
                context = listener.GetContext();
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileUploadServer] Exception in GetContext: {e}");
                continue;
            }

            ThreadPool.QueueUserWorkItem(o => ProcessRequest(context));
        }
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        if (request.HttpMethod != "POST")
        {
            response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            response.Close();
            return;
        }

        string contentType = request.ContentType;
        if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("multipart/form-data", StringComparison.InvariantCultureIgnoreCase))
        {
            response.StatusCode = (int)HttpStatusCode.BadRequest;
            WriteStringToResponse(response, "Expected multipart/form-data");
            return;
        }

        // Extract boundary from Content-Type header without converting whole body to string
        string boundary = null;
        foreach (var part in contentType.Split(';'))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("boundary=", StringComparison.InvariantCultureIgnoreCase))
            {
                boundary = "--" + trimmed.Substring("boundary=".Length);
                break;
            }
        }

        if (boundary == null)
        {
            response.StatusCode = (int)HttpStatusCode.BadRequest;
            WriteStringToResponse(response, "Missing boundary in Content-Type");
            return;
        }

        try
        {
            // Read the entire request body into a byte array
            using (var ms = new MemoryStream())
            {
                request.InputStream.CopyTo(ms);
                byte[] bodyBytes = ms.ToArray();
                byte[] boundaryBytes = Encoding.UTF8.GetBytes(boundary);

                // Find the start of the first part
                int pos = FindPattern(bodyBytes, boundaryBytes, 0);
                if (pos < 0)
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    WriteStringToResponse(response, "Invalid multipart format (no boundary found)");
                    return;
                }

                // Skip boundary + CRLF
                pos += boundaryBytes.Length + 2; // \r\n

                // Now parse headers of the first part to find filename
                // Headers end with \r\n\r\n
                int headerEnd = FindPattern(bodyBytes, Encoding.UTF8.GetBytes("\r\n\r\n"), pos);
                if (headerEnd < 0)
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    WriteStringToResponse(response, "Invalid multipart: headers not terminated properly");
                    return;
                }

                // Extract header bytes and convert only that portion to string
                int headerLength = headerEnd - pos;
                string headerString = Encoding.UTF8.GetString(bodyBytes, pos, headerLength);

                // Parse filename from headerString
                string filename = ParseFilenameFromHeader(headerString);
                if (string.IsNullOrEmpty(filename))
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    WriteStringToResponse(response, "Could not find filename in form-data");
                    return;
                }

                // File data starts immediately after \r\n\r\n
                int fileDataStart = headerEnd + 4; // skip \r\n\r\n
                // Find the end of file data: boundary preceded by \r\n
                int fileDataEnd = FindPattern(bodyBytes, Encoding.UTF8.GetBytes("\r\n" + boundary), fileDataStart);
                if (fileDataEnd < 0) fileDataEnd = bodyBytes.Length;

                int fileLength = fileDataEnd - fileDataStart;
                byte[] fileBytes = new byte[fileLength];
                Array.Copy(bodyBytes, fileDataStart, fileBytes, 0, fileLength);

                // Save into Assets/UploadedTextures so Unity can import it
                string uploadFolder = Path.Combine(Application.dataPath, "UploadedTextures");
                if (!Directory.Exists(uploadFolder))
                    Directory.CreateDirectory(uploadFolder);

                string savePath = Path.Combine(uploadFolder, filename);
                File.WriteAllBytes(savePath, fileBytes);

                Debug.Log($"[FileUploadServer] Saved file to {savePath}");

#if UNITY_EDITOR
                // Queue import for main thread
                string assetPath = $"Assets/UploadedTextures/{filename}";
                importQueue.Enqueue(assetPath);
#endif

                response.StatusCode = (int)HttpStatusCode.OK;
                WriteStringToResponse(response, $"Uploaded and saved as: {filename}");

                // Now image is saved, create the fish!

                // Extract the fish's name
                string[] fishNameBits = filename.Split("_");
                string fishName = fishNameBits[0];

                // Spawn the fish, telling spawner where the texture is.
                Debug.Log("Spawned one fish: " + fishName);
                fishSpawner.SpawnFish(fishName, savePath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FileUploadServer] Exception while processing request: {ex}");
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            WriteStringToResponse(response, "Server error: " + ex.Message);
        }
    }

    /// <summary>
    /// Parses the Content-Disposition header block to extract the filename value.
    /// Assumes headerBlock is ASCII/UTF8 and contains a line like:
    /// Content-Disposition: form-data; name="file"; filename="shark_AbCdE123.png"
    /// </summary>
    private string ParseFilenameFromHeader(string headerBlock)
    {
        const string cdToken = "Content-Disposition:";
        int cdIndex = headerBlock.IndexOf(cdToken, StringComparison.InvariantCultureIgnoreCase);
        if (cdIndex < 0) return null;

        int fnIndex = headerBlock.IndexOf("filename=\"", cdIndex, StringComparison.InvariantCultureIgnoreCase);
        if (fnIndex < 0) return null;

        int start = fnIndex + "filename=\"".Length;
        int end = headerBlock.IndexOf("\"", start);
        if (end < 0) return null;

        return headerBlock.Substring(start, end - start);
    }

    /// <summary>
    /// Finds the first occurrence of patternBytes in buffer, starting at offset.
    /// Returns the index, or -1 if not found.
    /// </summary>
    private int FindPattern(byte[] buffer, byte[] patternBytes, int startOffset)
    {
        for (int i = startOffset; i + patternBytes.Length <= buffer.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < patternBytes.Length; j++)
            {
                if (buffer[i + j] != patternBytes[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }

    private void WriteStringToResponse(HttpListenerResponse response, string message)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        response.ContentType = "text/plain";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = bytes.LongLength;
        using (var output = response.OutputStream)
        {
            output.Write(bytes, 0, bytes.Length);
        }
        response.Close();
    }
}
