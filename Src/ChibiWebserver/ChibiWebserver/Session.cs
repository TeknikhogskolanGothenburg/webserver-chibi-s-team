using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace ChibiWebserver
{
    class Session
    {
        private Dictionary<string, string> session;
        private int lastSessionIndex;
        private string thisRunsUniqueKey;
        private HttpListenerRequest request;
        private HttpListenerResponse response;

        /// <summary>
        /// Properties for session.count - get
        /// </summary>
        public int Count
        {
            get
            {
                return session.Count;
            }
        }

        /// <summary>
        /// Constructor with this run's unique sessions key
        /// </summary>
        /// <param name="key">This run's unique sessions key</param>
        public Session(string key)
        {
            session = new Dictionary<string, string>();
            thisRunsUniqueKey = key;
            lastSessionIndex = 0;
        }

        /// <summary>
        /// Initialize request and response from web request 
        /// </summary>
        /// <param name="context">Web context (HttpListenerContext)</param>
        public void Initialize(HttpListenerContext context)
        {
            request = context.Request;
            response = context.Response;
        }

        /// <summary>
        /// Add a session value
        /// </summary>
        /// <param name="value">Value to input (string)</param>
        public void Add(string value)
        {
            // Session key set by this run's unique key and last session index + one
            lastSessionIndex++;
            string sessionKey = string.Format("{0}_{1}", thisRunsUniqueKey, lastSessionIndex);

            // Set session and cookie
            session.Add(sessionKey, value);
            response.SetCookie(new Cookie("counter", sessionKey, "/"));
        }

        /// <summary>
        /// Check if session with cookie key exist
        /// </summary>
        /// <param name="cookieKey">cookie key (string)</param>
        /// <returns>Validation result (bool)</returns>
        public bool Exist(string cookieKey)
        {
            Cookie cookie = GetCookie(cookieKey);

            // If cookie exist, check if session with session key exist
            if(cookie != null)
            {
                string sessionKey = cookie.Value;

                if (session.ContainsKey(sessionKey))
                {
                    return true;
                }

                // If not existing expire cookie
                cookie.Expired = true;
            }

            return false;
        }

        /// <summary>
        /// Return session value by cookie key
        /// </summary>
        /// <param name="cookieKey">Cookie key (string)</param>
        /// <returns>Session value (string)</returns>
        public string Get(string cookieKey)
        {
            if (Exist(cookieKey))
            {
                string sessionKey = GetCookie(cookieKey).Value;

                return session[sessionKey];
            }

            return null;
        }

        /// <summary>
        /// Edit session value
        /// </summary>
        /// <param name="cookieKey">Cookie key (String)</param>
        /// <param name="newValue">New value (string)</param>
        /// <returns>Validation Result (bool)</returns>
        public bool Edit(string cookieKey, string newValue)
        {
            if (Exist(cookieKey))
            {
                string sessionKey = GetCookie(cookieKey).Value;

                session[sessionKey] = newValue;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get cookie by key from request or response, if not exist it return null
        /// </summary>
        /// <param name="cookieKey">Cookie key (string)</param>
        /// <returns>Cookie object</returns>
        private Cookie GetCookie(string cookieKey)
        {
            if ((request.Cookies[cookieKey] != null) && !request.Cookies[cookieKey].Expired)
            {
                return request.Cookies[cookieKey];
            }
            else if((response.Cookies[cookieKey] != null) && !response.Cookies[cookieKey].Expired)
            {
                return response.Cookies[cookieKey];
            }

            return null;
        }
    }
}
