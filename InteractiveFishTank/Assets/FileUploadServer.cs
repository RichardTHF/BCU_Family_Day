using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

public class FileUploadServer : MonoBehaviour
{
    // Port on which Unity will listen for uploads
    [Tooltip("Port number for the HTTP listener (e.g. 5000)")]
    public int listenPort = 5000;

    // URL prefix (you can bind to all interfaces with http://*:5000/ or localhost only)
    private string urlPrefix;

    // The HttpListener instance
    private HttpListener listener;
    private Thread listenerThread;

    void Start()
    {
        // Build a prefix that listens on all network interfaces:
        urlPrefix = $"http://*:{listenPort}/upload/";

        // Initialize and start the HttpListener in a background thread:
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
        // Stop the listener cleanly
        if (listener != null && listener.IsListening)
        {
            listener.Stop();
            listener.Close();
        }

        if (listenerThread != null && listenerThread.IsAlive)
            listenerThread.Abort();
    }

    /// <summary>
    /// The main loop that accepts HTTP requests and processes them.
    /// </summary>
    private void HandleIncomingConnections()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context = null;
            try
            {
                // Blocking call – will wait here until a request comes in
                context = listener.GetContext();
            }
            catch (HttpListenerException)
            {
                // Listener was stopped
                break;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileUploadServer] Exception in GetContext: {e}");
                continue;
            }

            // Handle the request on a threadpool thread to avoid blocking
            ThreadPool.QueueUserWorkItem(o => ProcessRequest(context));
        }
    }

    /// <summary>
    /// Processes a single HTTP request. Expects multipart/form-data with a single file field named "file".
    /// </summary>
    private void ProcessRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // Only accept POST
        if (request.HttpMethod != "POST")
        {
            response.StatusCode = (int)HttpStatusCode.MethodNotAllowed; // 405
            response.Close();
            return;
        }

        // Content‐Type should be multipart/form-data; boundary=----XYZ
        string contentType = request.ContentType;
        if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("multipart/form-data"))
        {
            response.StatusCode = (int)HttpStatusCode.BadRequest; // 400
            WriteStringToResponse(response, "Expected multipart/form-data");
            return;
        }

        // Extract boundary from Content-Type header:
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
            // Read entire request body into a byte[]
            using (var ms = new MemoryStream())
            {
                request.InputStream.CopyTo(ms);
                byte[] bodyBytes = ms.ToArray();

                // Parse the multipart form‐data to extract the file
                // We assume only one file part, named "file"
                // The format is like:
                // --<boundary>\r\n
                // Content-Disposition: form-data; name="file"; filename="shark_A1b2C3d4.png"\r\n
                // Content-Type: image/png\r\n
                // \r\n
                // <binary data>\r\n
                // --<boundary>--\r\n

                string bodyString = Encoding.UTF8.GetString(bodyBytes);
                // Find filename in header
                string filename = ParseFilename(bodyString);
                if (string.IsNullOrEmpty(filename))
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    WriteStringToResponse(response, "Could not find filename in form-data");
                    return;
                }

                // Split on boundary to isolate the file section
                string[] sections = bodyString.Split(new[] { boundary }, StringSplitOptions.None);
                if (sections.Length < 2)
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    WriteStringToResponse(response, "Invalid multipart format");
                    return;
                }

                // The file data is in the second section (index 1), between CRLFs
                int startOfFileData = bodyString.IndexOf("\r\n\r\n", bodyString.IndexOf(sections[1])) + 4;
                int endOfFileData = bodyString.IndexOf("\r\n", startOfFileData);
                int headerLength = startOfFileData - bodyString.IndexOf(sections[1]);

                // Calculate byte offsets:
                // Find offset of section[1] in the byte array
                int section1ByteOffset = FindByteIndex(bodyBytes, Encoding.UTF8.GetBytes(sections[1]));
                int fileDataStart = section1ByteOffset + headerLength;
                int fileDataEnd = FindByteIndex(bodyBytes, Encoding.UTF8.GetBytes("\r\n" + boundary), fileDataStart);
                if (fileDataEnd < 0) fileDataEnd = bodyBytes.Length;

                int fileLength = fileDataEnd - fileDataStart;
                byte[] fileBytes = new byte[fileLength];
                Array.Copy(bodyBytes, fileDataStart, fileBytes, 0, fileLength);

                // Save to disk (Application.persistentDataPath)
                string savePath = Path.Combine(Application.persistentDataPath, filename);
                File.WriteAllBytes(savePath, fileBytes);

                Debug.Log($"[FileUploadServer] Saved file to {savePath}");

                response.StatusCode = (int)HttpStatusCode.OK;
                WriteStringToResponse(response, $"Uploaded and saved as: {filename}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FileUploadServer] Exception while processing request: {ex}");
            response.StatusCode = (int)HttpStatusCode.InternalServerError; // 500
            WriteStringToResponse(response, "Server error: " + ex.Message);
        }
    }

    /// <summary>
    /// Helper to parse the “filename” attribute from a multipart Content‐Disposition header.
    /// </summary>
    private string ParseFilename(string part)
    {
        // Look for Content-Disposition line: filename="something.png"
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

    /// <summary>
    /// Searches for the first occurrence of patternBytes inside buffer, starting at offset.
    /// Returns the index (byte offset) or -1 if not found.
    /// </summary>
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

    /// <summary>
    /// Writes a simple text response and closes.
    /// </summary>
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
