using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Newtonsoft.Json;

/// <summary>
/// REST API helper.
/// </summary>
public class RestApi {
    #region Properties

    /// <summary>
    /// Base URL to add to each request.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Username for Basic Auth.
    /// </summary>
    public string BasicAuthUsername { get; set; }

    /// <summary>
    /// Password for Basic Auth.
    /// </summary>
    public string BasicAuthPassword { get; set; }

    /// <summary>
    /// Headers to add to each request.
    /// </summary>
    public Dictionary<string, string> GlobalHeaders { get; set; }

    /// <summary>
    /// SSL Certificate to add to each request.
    /// </summary>
    public X509Certificate2 ClientCertificate { get; set; }

    #endregion

    #region Helper objects

    /// <summary>
    /// Wrapper for a single request.
    /// </summary>
    public class RestApiWrapper {
        /// <summary>
        /// When the request started.
        /// </summary>
        public DateTime Start { get; set; }

        /// <summary>
        /// When the request ended.
        /// </summary>
        public DateTime End { get; set; }

        /// <summary>
        /// How long the request took.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Exception rised during execution, if any.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Keeper of the request information.
        /// </summary>
        public RestApiWrapperRequest Request { get; set; }

        /// <summary>
        /// Keeper of the response information.
        /// </summary>
        public RestApiWrapperResponse Response { get; set; }

        /// <summary>
        /// Keeper of the request information.
        /// </summary>
        public class RestApiWrapperRequest {
            /// <summary>
            /// URL to request.
            /// </summary>
            public string Url { get; set; }

            /// <summary>
            /// HTTP method to use.
            /// </summary>
            public string Method { get; set; }

            /// <summary>
            /// Body to transmit.
            /// </summary>
            public string Body { get; set; }

            /// <summary>
            /// Headers trasmitted.
            /// </summary>
            public Dictionary<string, string> Headers { get; set; }

            /// <summary>
            /// Basic Auth username.
            /// </summary>
            public string BasicAuthUsername { get; set; }

            /// <summary>
            /// Basic Auth password.
            /// </summary>
            public string BasicAuthPassword { get; set; }

            /// <summary>
            /// SSL Certificate.
            /// </summary>
            public X509Certificate2 ClientCertificate { get; set; }
        }

        /// <summary>
        /// Keeper of the response information.
        /// </summary>
        public class RestApiWrapperResponse {
            /// <summary>
            /// Response status code.
            /// </summary>
            public HttpStatusCode StatusCode { get; set; }

            /// <summary>
            /// Response status description.
            /// </summary>
            public string StatusDescription { get; set; }

            /// <summary>
            /// Response headers.
            /// </summary>
            public Dictionary<string, string> Headers { get; set; }

            /// <summary>
            /// Response body.
            /// </summary>
            public string Body { get; set; }

            /// <summary>
            /// Convert the response body to given type.
            /// </summary>
            /// <typeparam name="T">Type to convert to.</typeparam>
            /// <returns>Converted content.</returns>
            public T BodyTo<T>() {
                try {
                    return JsonConvert.DeserializeObject<T>(this.Body);
                }
                catch {
                    return default(T);
                }
            }
        }
    }

    #endregion

    #region Main functions

    /// <summary>
    /// Make a single request.
    /// </summary>
    /// <param name="url">URL to request.</param>
    /// <param name="method">HTTP method to use.</param>
    /// <param name="body">Body to transmit.</param>
    /// <param name="headers">Headers to transmit.</param>
    /// <returns>Wrapper for request attempt.</returns>
    public RestApiWrapper Request(string url, string method, object body = null,
        Dictionary<string, string> headers = null) {
        var wrapper = new RestApiWrapper {
            Start = DateTime.Now,
            Request = new RestApiWrapper.RestApiWrapperRequest {
                Url = this.BaseUrl + url,
                Method = method.ToUpper(),
                Headers = this.GlobalHeaders,
                BasicAuthUsername = this.BasicAuthUsername,
                BasicAuthPassword = this.BasicAuthPassword,
                ClientCertificate = this.ClientCertificate
            }
        };

        // Add given headers.
        if (headers != null) {
            if (wrapper.Request.Headers == null) {
                wrapper.Request.Headers = new Dictionary<string, string>();
            }

            foreach (var header in headers) {
                wrapper.Request.Headers.Add(header.Key, header.Value);
            }
        }

        // Prepare body.
        if (body != null) {
            if (body is string) {
                wrapper.Request.Body = body.ToString();
            }
            else {
                wrapper.Request.Body = JsonConvert.SerializeObject(body);
            }
        }

        try {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            Stream stream;
            var req = WebRequest.Create(wrapper.Request.Url) as HttpWebRequest;

            if (req == null) {
                throw new Exception("Unable to create a HttpWebRequest.");
            }

            req.Method = wrapper.Request.Method;
            req.UserAgent = "RestApiWrapper";

            // Add basic auth.
            if (!string.IsNullOrWhiteSpace(wrapper.Request.BasicAuthUsername) &&
                !string.IsNullOrWhiteSpace(wrapper.Request.BasicAuthPassword)) {
                var b64e = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        string.Format("{0}:{1}",
                            wrapper.Request.BasicAuthUsername,
                            wrapper.Request.BasicAuthPassword)));

                req.Headers.Add(
                    "Authorization",
                    string.Format("Basic {0}",
                        b64e));
            }

            // Add client certificate.
            if (wrapper.Request.ClientCertificate != null) {
                req.ClientCertificates.Add(wrapper.Request.ClientCertificate);
            }

            // Add body.
            if (!string.IsNullOrWhiteSpace(wrapper.Request.Body)) {
                var bytes = Encoding.UTF8.GetBytes(wrapper.Request.Body);

                req.ContentLength = bytes.Length;
                req.ContentType = "application/json; charset=utf-8";

                stream = req.GetRequestStream();

                stream.Write(bytes, 0, bytes.Length);
                stream.Close();
            }

            // Store request headers.
            if (wrapper.Request.Headers == null) {
                wrapper.Request.Headers = new Dictionary<string, string>();
            }

            foreach (var key in req.Headers.AllKeys) {
                if (wrapper.Request.Headers.ContainsKey(key)) {
                    continue;
                }

                wrapper.Request.Headers.Add(key, req.Headers[key]);
            }

            // Get response.
            var res = req.GetResponse() as HttpWebResponse;

            if (res == null) {
                throw new Exception("Unable to get HttpWebResponse.");
            }

            if (wrapper.Response == null) {
                wrapper.Response = new RestApiWrapper.RestApiWrapperResponse();
            }

            wrapper.Response.StatusCode = res.StatusCode;
            wrapper.Response.StatusDescription = res.StatusDescription;

            // Get response headers.
            if (res.Headers.AllKeys.Any()) {
                if (wrapper.Response.Headers == null) {
                    wrapper.Response.Headers = new Dictionary<string, string>();
                }

                foreach (var key in res.Headers.AllKeys) {
                    wrapper.Response.Headers.Add(key, res.Headers[key]);
                }
            }

            // Get response body.
            stream = res.GetResponseStream();

            if (stream != null) {
                var reader = new StreamReader(stream, Encoding.UTF8);
                wrapper.Response.Body = reader.ReadToEnd();
            }
        }
        catch (WebException ex) {
            wrapper.Exception = ex;

            var res = ex.Response as HttpWebResponse;

            if (res != null) {
                if (wrapper.Response == null) {
                    wrapper.Response = new RestApiWrapper.RestApiWrapperResponse();
                }

                wrapper.Response.StatusCode = res.StatusCode;
                wrapper.Response.StatusDescription = res.StatusDescription;

                var stream = res.GetResponseStream();

                if (stream != null) {
                    var reader = new StreamReader(stream, Encoding.UTF8);
                    wrapper.Response.Body = reader.ReadToEnd();
                }
            }
        }
        catch (Exception ex) {
            wrapper.Exception = ex;
        }

        wrapper.End = DateTime.Now;
        wrapper.Duration = wrapper.End - wrapper.Start;

        return wrapper;
    }

    #endregion

    #region Short-hand functions

    /// <summary>
    /// Short-hand function for a 'GET' request.
    /// </summary>
    /// <param name="url">URL to request.</param>
    /// <param name="headers">Headers to transmit.</param>
    /// <returns>Wrapper for request attempt.</returns>
    public RestApiWrapper Get(string url = null, Dictionary<string, string> headers = null) {
        return Request(url, "GET", null, headers);
    }

    /// <summary>
    /// Short-hand function for a 'POST' request.
    /// </summary>
    /// <param name="url">URL to request.</param>
    /// <param name="body">Body to transmit.</param>
    /// <param name="headers">Headers to transmit.</param>
    /// <returns>Wrapper for request attempt.</returns>
    public RestApiWrapper Post(string url = null, object body = null, Dictionary<string, string> headers = null) {
        return Request(url, "POST", body, headers);
    }

    /// <summary>
    /// Short-hand function for a 'PUT' request.
    /// </summary>
    /// <param name="url">URL to request.</param>
    /// <param name="body">Body to transmit.</param>
    /// <param name="headers">Headers to transmit.</param>
    /// <returns>Wrapper for request attempt.</returns>
    public RestApiWrapper Put(string url = null, object body = null, Dictionary<string, string> headers = null) {
        return Request(url, "PUT", body, headers);
    }

    /// <summary>
    /// Short-hand function for a 'DELETE' request.
    /// </summary>
    /// <param name="url">URL to request.</param>
    /// <param name="body">Body to transmit.</param>
    /// <param name="headers">Headers to transmit.</param>
    /// <returns>Wrapper for request attempt.</returns>
    public RestApiWrapper Delete(string url = null, object body = null, Dictionary<string, string> headers = null) {
        return Request(url, "DELETE", body, headers);
    }

    /// <summary>
    /// Short-hand function for a 'HEAD' request.
    /// </summary>
    /// <param name="url">URL to request.</param>
    /// <param name="headers">Headers to transmit.</param>
    /// <returns>Wrapper for request attempt.</returns>
    public RestApiWrapper Head(string url = null, Dictionary<string, string> headers = null) {
        return Request(url, "HEAD", null, headers);
    }

    #endregion
}