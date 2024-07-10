using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace COREBusinessSvc.Utilities
{
    public class CommonUtilities
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static string GetRequest(Uri uri)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            var httpClient = new HttpClient();
            // our return variable
            string response = string.Empty;
            try
            {
                var httpRequestMessage = new HttpRequestMessage();
                httpRequestMessage.Method = HttpMethod.Get;
                httpRequestMessage.RequestUri = uri;

                using (var httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result)
                {
                    if (httpResponseMessage.StatusCode != HttpStatusCode.OK)
                    {
                        //log error
                    }
                    else
                    {
                        using (var stream = httpResponseMessage.Content.ReadAsStreamAsync().Result)
                        {
                            using (var streamReader = new StreamReader(stream))
                            {
                                response = streamReader.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                response = "";
            }
            return response;
        }

        public async static Task<string> PostRequest(Uri uri, string urlRoute, string requestContent, string contentType, int timeout, int retry, IConfiguration Configuration, IHttpClientFactory clientFactory)
        {
            DateTime start_time = DateTime.Now;

            logger.Debug(string.Format("POST Request sent for {0}. Request started at: {1}", uri.ToString(), start_time));

            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            // our return variable
            string response = string.Empty;

            try
            {
                for (int i = 0; i < retry; i++)
                {
                    try
                    {
                        logger.Debug("REQUEST BODY: " + requestContent);
                        var httpContent = new StringContent(requestContent, Encoding.UTF8, "application/json");

                        var client =
                            new HttpClient(
                                new HttpClientHandler
                                {
                                    AutomaticDecompression = DecompressionMethods.GZip
                                                             | DecompressionMethods.Deflate
                                });


                        //Setting factory client details
                        if (uri.ToString().Contains("dcms"))
                        {
                            client = clientFactory.CreateClient("DCMSPostMethod");
                        }
                        else if (uri.ToString().Contains("rule"))
                        {
                            client = clientFactory.CreateClient("RulesPostMethod");
                        }
                        else if (uri.ToString().Contains("reference"))
                        {
                            client = clientFactory.CreateClient("ReferencePostMethod");
                        }

                        using (var httpResponseMessage = await client.PostAsync(urlRoute, httpContent))
                        {
                            DateTime end_time = DateTime.Now;
                            TimeSpan elapsed_time = end_time - start_time;
                            logger.Debug(string.Format("POST Response recieved for {0}; Response recieved at: {1}; Elapsed time for req/res: {2} ms", uri.ToString(), end_time, elapsed_time.TotalMilliseconds));

                            if (httpResponseMessage.StatusCode != HttpStatusCode.OK)
                            {
                                throw new HttpRequestException($"HttpRequest exception thrown - url: {uri.ToString()}, status code: {httpResponseMessage.StatusCode} ");
                            }
                            else
                            {
                                using (var stream = httpResponseMessage.Content.ReadAsStreamAsync().Result)
                                {
                                    using (var streamReader = new StreamReader(stream))
                                    {
                                        response = streamReader.ReadToEnd();

                                        if (Configuration["AdvancedLogging"] != "false")
                                        {
                                            logger.Debug("RESPONSE HEADER: " + httpResponseMessage);
                                            logger.Debug("RESPONSE BODY: " + response);
                                        }
                                    }
                                }
                            }
                        }


                        // break out of the for loop because we got a response
                        break;
                    }
                    catch (WebException)
                    {
                        if (i == retry - 1)
                            throw;
                    }
                    catch (AggregateException ae)
                    {
                        if (i == retry - 1)
                        {
                            ae.Handle((x) =>
                            {
                                if (x is TaskCanceledException)
                                    throw new TaskCanceledException("The task was canceled after 3 attempts");
                                return false;
                            });
                        }
                    }
                    catch (HttpRequestException)
                    {
                        if (i == retry - 1)
                            throw;
                    }
                    catch (Exception ex)
                    {
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                response = "";
            }


            return response;

        }

        private static HttpRequestMessage CreateHttpRequest(HttpMethod httpMethod, Uri uri, string requestContent, string contentType)
        {
            var httpRequest = new HttpRequestMessage(httpMethod, uri);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Accept-Encoding", "gzip, deflate");

            // read the data we are sending into a byte array and set the content type. 
            // content types with our requests are either application/json or text/xml
            byte[] byteArray = Encoding.UTF8.GetBytes(requestContent);
            HttpContent content = new ByteArrayContent(byteArray, 0, byteArray.Length);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            httpRequest.Content = content;

            return httpRequest;
        }

        //public void passwordChanger() 
        //{
        //    const string ADS_SECURE_AUTHENTICATION = "1";
        //    const string ADS_USE_ENCRYPTION = "2";

        //    logger.Debug("Start pw change");
        //    string strPath = "LDAP://ldaps.tus.ams1907.com:636/cn=<account>,ou=Users,ou=SysMgt,dc=tus,dc=ams1907,dc=com"
        //    string strUser = "LRW6LYX";
        //    string strPassword = "";
        //    string voldpw = null, vnewpw = null;
        //    logger.Debug(string.Format("Old PWD func: {0}", voldpw));
        //    logger.Debug(string.Format("New PWD func: {0}", vnewpw));

        //    strPassword = voldpw;

        //    Set objDSO = GetObject("LDAP:")
        //    Set objUser = objDSO.OpenDSObject(strPath, strUser, strPassword, ADS_USE_ENCRYPTION OR ADS_SECURE_AUTHENTICATION)

        //    wscript.echo "ccc " & err.number & ", " & Err.Description
        //    output = objUser.ChangePassword(voldpw, vnewpw)
        //    wscript.Echo "PWLC: " & objuser.PasswordLastChanged
        //    wscript.echo "End pw change func"
        //    setpsswd = output


        //}

        public string RemoveSpecialCharacters(string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static bool CheckIfCallHasRequiredHeader(IHeaderDictionary requestHeaders)
        {
            bool validRequest = false;

            if (requestHeaders.TryGetValue("App-Key", out var appKeyHeaderValue))
            {
                if (appKeyHeaderValue.ToString() == "ERA")
                {
                    validRequest = true;
                }
            }

            return validRequest;
        }

    }

}
