/// Based on:
/// http://www.codingvision.net/networking/c-simple-http-server
/// https://www.codehosting.net/blog/BlogEngine/post/Simple-C-Web-Server
/// https://gist.github.com/aksakalli/9191056
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using System.Security.Cryptography;

namespace ChibiWebserver
{
    class WebServer
    {
        private HttpListener listener;
        private string rootDirectory;
        private Thread serverThread;

        // Index files, to show on a directory root
        private readonly string[] indexFiles = {
            "index.html",
            "index.htm",
            "default.html",
            "default.htm"
        };

        // Allowed files extensions with mime mapping
        private readonly string[] allowedFileExtensions = new string[]
        {
            ".css", ".js", ".htm", ".html", ".shtml",
            ".rss", ".xml", ".pdf", ".txt", ".csv",
            ".png", ".gif", ".jpeg", ".jpg", ".jng",
            ".ico"
        };

        /// <summary>
        /// Constructor with prefixes
        /// </summary>
        /// <param name="prefixes">Prefixes for server (String[])</param>
        public WebServer(string[] prefixes)
        {
            if (!HttpListener.IsSupported)
            {
                Console.WriteLine("Windows XP SP2 or Server 2003 is required to use the HttpListener class.");
                return;
            }

            // Set root directory
            rootDirectory = Directory.GetCurrentDirectory() + "/../../../../../Content/";

            // URI prefixes are required,
            // for example "http://localhost.com:8080/".
            if (prefixes == null || prefixes.Length == 0)
                throw new ArgumentException("prefixes");

            // Create a listener.
            listener = new HttpListener();

            // Add the prefixes.
            foreach (string s in prefixes)
            {
                listener.Prefixes.Add(s);
            }
        }

        /// <summary>
        /// Start webserver
        /// </summary>
        public void Start()
        {
            listener.Start();

            Console.WriteLine("Webserver is running...");

            serverThread = new Thread(this.Listen);
            serverThread.Start();
        }

        /// <summary>
        /// Listen to web ´requests
        /// </summary>
        public void Listen()
        {
            try
            {
                // Loop and process browser request, as long as HtmlListener is active
                while (listener.IsListening)
                {
                    // Note: The GetContext method blocks while waiting for a request.
                    HttpListenerContext context = listener.GetContext();

                    Process(context);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Process browser request
        /// </summary>
        public void Process(HttpListenerContext context)
        {
            // Obtain request and response objects.
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            // Get a response stream
            Stream output = response.OutputStream;

            // Web path and filename ex. blog/index.html
            string webpath = context.Request.Url.AbsolutePath;
            webpath = webpath.Substring(1);

            string fileSource = GetRealFileSource(ref webpath);

            Console.Write("Request on " + webpath + " return ");

            // Does file exist?
            if (File.Exists(fileSource))
            {
                string fileExtension = Path.GetExtension(webpath);

                // Check if file extension is allowed
                if (allowedFileExtensions.Contains(fileExtension))
                {
                    try
                    {
                        DateTime date = DateTime.Now;
                        DateTime expiresDate = date.Add(new TimeSpan(365, 0, 0, 0)); // Year from now

                        // Read data from file
                        byte[] fileData = File.ReadAllBytes(fileSource);

                        Console.WriteLine("200");

                        // Construct headers
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.ContentType = MimeMapping.GetMimeMapping(fileSource);
                        response.ContentLength64 = fileData.Length;
                        response.AddHeader("Date", date.ToString("r"));
                        response.AddHeader("Expires", expiresDate.ToString("r"));
                        response.AddHeader("Last-Modified", File.GetLastWriteTime(fileSource).ToString("r"));
                        response.AddHeader("Etag", MD5_Checksum(fileData));

                        // Write the response to output stream.
                        output.Write(fileData, 0, fileData.Length);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("500");
                        response.StatusCode = (int)HttpStatusCode.InternalServerError; // 500
                    }
                }
                else
                {
                    Console.WriteLine("415");
                    response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType; // 415
                }
            }
            else
            {
                Console.WriteLine("404");
                response.StatusCode = (int)HttpStatusCode.NotFound; // 404
            }

            // You must close the output stream.
            output.Close();
        }

        /// <summary>
        /// Stop webserver
        /// </summary>
        public void Stop()
        {
            listener.Stop();
            listener.Close();
            Console.WriteLine("Webserver is shutdown");
        }

        /// <summary>
        /// Return whole source to file 
        /// </summary>
        /// <param name="webpath">Path and/or visit in browser (string)</param>
        /// <returns>Whole source to file (string)</returns>
        private string GetRealFileSource(ref string webpath)
        {
            string fileSource = Path.Combine(rootDirectory, webpath);

            // If webpath are source to directory, check if any default page exist in this directory
            if (IsDirectory(fileSource))
            {
                foreach (string indexFile in indexFiles)
                {
                    //Console.WriteLine(Path.Combine(fileSource, indexFile));
                    if (File.Exists(Path.Combine(fileSource, indexFile)))
                    {
                        webpath = webpath + indexFile;
                        fileSource = Path.Combine(rootDirectory, webpath);
                        break;
                    }
                }
            }

            return fileSource;
        }

        /// <summary>
        /// Check if source is to directory
        /// </summary>
        /// <param name="path">Websource (string)</param>
        /// <returns>Validate result</returns>
        private bool IsDirectory(string path)
        {
            if (Directory.Exists(path)) // is a directory
                return true;
            else
                return false;
        }

        /// <summary>
        /// Make a MD5 checksum on fileData
        /// </summary>
        /// <param name="fileData">file bytes (Byte[])</param>
        /// <returns>MD5 checksum as string</returns>
        private string MD5_Checksum(Byte[] fileData)
        {
            using (var md5 = MD5.Create())
            {
                // Hash to MD5, convert to string and remove seperating lines
                return BitConverter.ToString(md5.ComputeHash(fileData)).Replace("-", "");
            }
        }
    }
}
