using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
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
        if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("multipart/form-data"))
        {
            response.StatusCode = (int)HttpStatusCode.BadRequest;
            WriteStringToResponse(response, "Expected multipart/form-data");
            return;
        }

        string boundary = null;
        string[] cts = contentType.Split(';');
        foreach (var part in cts)
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
            using (var ms = new MemoryStream())
            {
                request.InputStream.CopyTo(ms);
                byte[] bodyBytes = ms.ToArray();
                string bodyString = Encoding.UTF8.GetString(bodyBytes);

                string filename = ParseFilename(bodyString);
                if (string.IsNullOrEmpty(filename))
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    WriteStringToResponse(response, "Could not find filename in form-data");
                    return;
                }

                string[] sections = bodyString.Split(new[] { boundary }, StringSplitOptions.None);
                if (sections.Length < 2)
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    WriteStringToResponse(response, "Invalid multipart format");
                    return;
                }

                int startOfFileData = bodyString.IndexOf("\r\n\r\n", bodyString.IndexOf(sections[1])) + 4;
                int headerLength = startOfFileData - bodyString.IndexOf(sections[1]);

                int section1ByteOffset = FindByteIndex(bodyBytes, Encoding.UTF8.GetBytes(sections[1]));
                int fileDataStart = section1ByteOffset + headerLength;
                int fileDataEnd = FindByteIndex(bodyBytes, Encoding.UTF8.GetBytes("\r\n" + boundary), fileDataStart);
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
                // Refresh the AssetDatabase so Unity imports the new PNG
                string assetPath = $"Assets/UploadedTextures/{filename}";
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                Debug.Log($"[FileUploadServer] Asset imported at {assetPath}");
#endif

                response.StatusCode = (int)HttpStatusCode.OK;
                WriteStringToResponse(response, $"Uploaded and saved as: {filename}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FileUploadServer] Exception while processing request: {ex}");
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            WriteStringToResponse(response, "Server error: " + ex.Message);
        }
    }

    private string ParseFilename(string part)
    {
        const string cdToken = "Content-Disposition:";
        int cdIndex = part.IndexOf(cdToken, StringComparison.InvariantCultureIgnoreCase);
        if (cdIndex < 0) return null;

        int fnIndex = part.IndexOf("filename=\"", cdIndex, StringComparison.InvariantCultureIgnoreCase);
        if (fnIndex < 0) return null;

        int start = fnIndex + "filename=\"".Length;
        int end = part.IndexOf("\"", start);
        if (end < 0) return null;

        return part.Substring(start, end - start);
    }

    private int FindByteIndex(byte[] buffer, byte[] patternBytes, int startOffset = 0)
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
