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
using System.Threading.Tasks;
using System.Web;
using System.Security.Cryptography;

namespace ChibiWebserver
{
    class WebServer
    {
        private HttpListener listener;

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

            // URI prefixes are required,
            // for example "http://contoso.com:8080/index/".
            if (prefixes == null || prefixes.Length == 0)
                throw new ArgumentException("prefixes");

            // Create a listener.
            listener = new HttpListener();

            // Add the prefixes.
            foreach (string s in prefixes)
            {
                listener.Prefixes.Add(s);
            }

            listener.Start();

            Console.WriteLine("Webserver is running...");
        }

        /// <summary>
        /// Run webserver
        /// </summary>
        public void Run()
        {
            ThreadPool.QueueUserWorkItem((o) =>
            {
                try
                {
                    while (listener.IsListening)
                    {
                        // Note: The GetContext method blocks while waiting for a request. 
                        HttpListenerContext context = listener.GetContext();
                        HttpListenerRequest request = context.Request;

                        // Obtain a response object.
                        HttpListenerResponse response = context.Response;

                        // Get a response stream
                        Stream output = response.OutputStream;

                        // Set root directory
                        string rootDirectory = Directory.GetCurrentDirectory() + "/../../../../../Content/";

                        // Url path and filename ex. blog/index.html
                        string webpath = context.Request.Url.AbsolutePath;
                        webpath = webpath.Substring(1);

                        string fileSource = Path.Combine(rootDirectory, webpath);

                        // If webpath are source to path, check if any default page exist
                        if (IsDirectory(fileSource))
                        {
                            foreach (string indexFile in indexFiles)
                            {
                                if (File.Exists(Path.Combine(rootDirectory, indexFile)))
                                {
                                    webpath += indexFile;
                                    fileSource = Path.Combine(rootDirectory, webpath);
                                    break;
                                }
                            }
                        }

                        Console.WriteLine("Request on " + fileSource);

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
                                    DateTime expiresDate = date.Add(new TimeSpan(365, 0, 0, 0));

                                    // Read data from file
                                    byte[] fileData = File.ReadAllBytes(fileSource);

                                    // Construct header
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
                                    response.StatusCode = (int)HttpStatusCode.InternalServerError; // 500
                                }
                            }
                            else
                            {
                                response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType; // 415
                            }
                        }
                        else
                        {
                            response.StatusCode = (int)HttpStatusCode.NotFound; // 404
                        }

                        // You must close the output stream.
                        output.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });
        }

        /// <summary>
        /// Stop webserver
        /// </summary>
        public void Stop()
        {
            listener.Stop();
            listener.Close();
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
