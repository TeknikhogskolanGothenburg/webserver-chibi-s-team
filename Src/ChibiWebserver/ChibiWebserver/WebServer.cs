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
        private Session session;
        private DateTime lastUpStart;
        private int CounterSinceRestart;

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

        // Existing server special pages
        private readonly string[] specialPages = new string[]
        {
            "counter/", "dynamic/", "info/"
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

            // Set server up start time
            lastUpStart = DateTime.Now;

            // Declare session object
            session = new Session(lastUpStart.ToString("yyyyMMddHHmmssfff"));

            // Set counter to zero
            CounterSinceRestart = 0;
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
        /// Stop webserver
        /// </summary>
        public void Stop()
        {
            listener.Stop();
            listener.Close();
            Console.WriteLine("Webserver is shutdown");
        }

        /// <summary>
        /// Listen to web requests
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
        /// <param name="context">Web context (HttpListenerContext)</param>
        public void Process(HttpListenerContext context)
        {
            // Obtain Request and Response objects.
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            // Get a response stream
            Stream output = response.OutputStream;

            // Initialize session
            session.Initialize(context);

            // Web path and filename ex. blog/index.html
            string webpath = request.Url.AbsolutePath;
            webpath = webpath.Substring(1);

            CounterSinceRestart++;

            // Check if we got existing counter session, then increase value
            if (session.Exist("counter"))
            {
                int counter;

                if (int.TryParse(session.Get("counter"), out counter))
                {
                    counter++;
                }
                else
                {
                    counter = 1;
                }

                session.Edit("counter", counter.ToString());
            }
            else
            {
                session.Add("1");
            }

            try
            {
                if (specialPages.Contains(webpath))
                {
                    SpecialPageResponse(context, webpath);
                }
                else
                {
                    FileResponse(context, webpath);
                }
            }
            catch
            {
                WriteLog(webpath, 500);
                response.StatusCode = (int)HttpStatusCode.InternalServerError; // 500

                // You must close the output stream.
                output.Close();
            }
        }

        /// <summary>
        /// Response special page
        /// </summary>
        /// <param name="context">Web context (HttpListenerContext)</param>
        /// <param name="webpath">Request web path (string)</param>
        public void SpecialPageResponse(HttpListenerContext context, string webpath)
        {
            // Obtain request and response objects.
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            // Get a response stream
            Stream output = response.OutputStream;

            string responseText = null;

            switch (webpath)
            {
                case "counter/":

                    string counter = session.Get("counter");
                    responseText = string.Format("<html><body>{0}</body></html>", counter);

                    response.ContentType = "text/html";

                    break;
                case "dynamic/":

                    int sum = 0;

                    int input1;
                    int input2;

                    if (request.QueryString["input1"] != null)
                    {
                        if (int.TryParse(request.QueryString["input1"], out input1))
                        {
                            sum = input1;
                        }
                    }

                    if (request.QueryString["input2"] != null)
                    {
                        if (int.TryParse(request.QueryString["input2"], out input2))
                        {
                            sum += input2;
                        }
                    }

                    if (request.Headers.Get("Accept") == "application/xml")
                    {
                        responseText = string.Format("<result><value>{0}</value></result>", sum);
                        response.ContentType = "application/xml";
                    }
                    else
                    {
                        responseText = string.Format("<html><body>{0}</body></html>", sum);
                        response.ContentType = "text/html";
                    }

                    break;

                case "info/":

                    string lastRestart = lastUpStart.ToString("yyyy-MM-dd hh:mm:ss");
                    responseText = string.Format("<html><body>Last restart: {0}<br />Up time: {1}<br />Total requests since restart: {2}<br />Sessions count: {3}</body></html>", lastRestart, UpTime(), CounterSinceRestart, session.Count);

                    response.ContentType = "text/html";
                    break;

                default:

                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    WriteLog(webpath, 404);

                    break;
            }
            
            if(responseText != null)
            {
                byte[] textBuffer = Encoding.UTF8.GetBytes(responseText);
                response.ContentLength64 = textBuffer.Length;
                output.Write(textBuffer, 0, textBuffer.Length);
                WriteLog(webpath, 200);
            }

            // You must close the output stream.
            output.Close();
        }

        /// <summary>
        /// File request, response with file if it exist
        /// </summary>
        /// <param name="context">Web context (HttpListenerContext)</param>
        /// <param name="webpath">Request web path (string)</param>
        private void FileResponse(HttpListenerContext context, string webpath)
        {
            // Obtain request and response objects.
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            // Get a response stream
            Stream output = response.OutputStream;

            string fileSource = GetRealFileSource(ref webpath);

            // Does file exist?
            if (File.Exists(fileSource))
            {
                string fileExtension = Path.GetExtension(webpath);

                // Check if file extension is allowed
                if (allowedFileExtensions.Contains(fileExtension))
                {
                    DateTime date = DateTime.Now;
                    DateTime expiresDate = date.Add(new TimeSpan(365, 0, 0, 0)); // Year from now

                    // Read data from file
                    byte[] fileData = File.ReadAllBytes(fileSource);

                    // Construct headers
                    response.ContentType = MimeMapping.GetMimeMapping(fileSource);
                    response.ContentLength64 = fileData.Length;
                    response.AddHeader("Date", date.ToString("r"));
                    response.AddHeader("Expires", expiresDate.ToString("r"));
                    response.AddHeader("Last-Modified", File.GetLastWriteTime(fileSource).ToString("r"));
                    response.AddHeader("Etag", MD5Hash(fileData));

                    // Write the response to output stream.
                    output.Write(fileData, 0, fileData.Length);
                    WriteLog(webpath, 200);
                }
                else
                {
                    WriteLog(webpath, 415);
                    response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
                }
            }
            else
            {
                WriteLog(webpath, 404);
                response.StatusCode = (int)HttpStatusCode.NotFound;
            }

            // You must close the output stream.
            output.Close();
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
        /// Write request with status code the serverlog
        /// </summary>
        /// <param name="webpath">Requested Web path (string)</param>
        /// <param name="statusCode">Returning status code (int)</param>
        public void WriteLog(string webpath, int statusCode)
        {
            Console.WriteLine(string.Format("Request on {0} return {1}", webpath, statusCode));
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
        private string MD5Hash(Byte[] fileData)
        {
            using (var md5 = MD5.Create())
            {
                // Hash to MD5, convert to string and remove seperating lines
                return BitConverter.ToString(md5.ComputeHash(fileData)).Replace("-", "");
            }
        }

        /// <summary>
        /// Return server up time
        /// </summary>
        /// <returns>Server up time (string)</returns>
        private string UpTime()
        {
            TimeSpan dateDiffrence = DateTime.Now - lastUpStart;
            int days = (dateDiffrence).Days;
            int hours = (dateDiffrence).Hours;
            int minutes = (dateDiffrence).Minutes;
            int seconds = (dateDiffrence).Seconds;

            return string.Format("Days {0} | Hours {1} | Minutes {2} | Seconds {3}", days, hours, minutes, seconds);
        }
    }
}
