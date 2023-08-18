// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using Microsoft.Datasync.Client.Authentication;
using Microsoft.Datasync.Client.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Datasync.Client.Http
{
    /// <summary>
    /// An internal version of the <see cref="HttpClient"/> class that provides
    /// pipeline policies and standardized headers.
    /// </summary>
    public class ServiceHttpClient : IDisposable
    {
        /// <summary>
        /// The protocol version this library implements.
        /// </summary>
        protected const string ProtocolVersion = "3.0.0";

        /// <summary>
        /// The root of the HTTP message handler pipeline.
        /// </summary>
        protected HttpMessageHandler roothandler;

        /// <summary>
        /// The <see cref="HttpClient"/> to use for communication.
        /// </summary>
        protected HttpClient client;

        /// <summary>
        /// A factory method for creating the default <see cref="HttpClientHandler"/>.
        /// </summary>
        protected Func<HttpMessageHandler> DefaultHandlerFactory = GetDefaultHttpClientHandler;

        /// <summary>
        /// Create a new <see cref="ServiceHttpClient"/> that communicates
        /// with the provided Datasync service endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint of the Datasync service.</param>
        /// <param name="clientOptions">The client options to use in configuring the HTTP client.</param>
        internal ServiceHttpClient(Uri endpoint, DatasyncClientOptions clientOptions) : this(endpoint, null, clientOptions)
        {
        }

        /// <summary>
        /// Create a new <see cref="ServiceHttpClient"/> that communicates
        /// with the provided Datasync service endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint of the Datasync service.</param>
        /// <param name="authenticationProvider">The authentication provider to use (if any)</param>
        /// <param name="clientOptions">The client options to use in configuring the HTTP client.</param>
        internal ServiceHttpClient(Uri endpoint, AuthenticationProvider authenticationProvider, DatasyncClientOptions clientOptions)
        {
            Arguments.IsValidEndpoint(endpoint, nameof(endpoint));
            Arguments.IsNotNull(clientOptions, nameof(clientOptions));

            Endpoint = endpoint;
            InstallationId = clientOptions.InstallationId;

            roothandler = CreatePipeline(clientOptions.HttpPipeline ?? Array.Empty<HttpMessageHandler>());
            if (authenticationProvider != null)
            {
                authenticationProvider.InnerHandler = roothandler;
                roothandler = authenticationProvider;
            }
            client = new HttpClient(roothandler)
            {
                BaseAddress = Endpoint,
                Timeout = clientOptions.HttpTimeout ?? TimeSpan.FromSeconds(100)
            };
            client.DefaultRequestHeaders.Add(ServiceHeaders.ProtocolVersion, ProtocolVersion);
            if (!string.IsNullOrWhiteSpace(clientOptions.UserAgent))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(ServiceHeaders.UserAgent, clientOptions.UserAgent);
                client.DefaultRequestHeaders.Add(ServiceHeaders.InternalUserAgent, clientOptions.UserAgent);
            }
            if (!string.IsNullOrWhiteSpace(clientOptions.InstallationId))
            {
                client.DefaultRequestHeaders.Add(ServiceHeaders.InstallationId, InstallationId);
            }
        }

        /// <summary>
        /// The endpoint of the Datasync service.
        /// </summary>
        public Uri Endpoint { get; }

        public string InstallationId { get; }

        /// <summary>
        /// Sends a request through the HTTP pipeline to the remote service asynchronously.
        /// </summary>
        /// <param name="requestMessage">The <see cref="HttpRequestMessage"/> to send to the service.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        /// <returns>A task that returns a <see cref="HttpResponseMessage"/> when complete.</returns>
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage requestMessage, CancellationToken cancellationToken = default)
        {
            Arguments.IsNotNull(requestMessage, nameof(requestMessage));
            try
            {
                return await client.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException();
            }
        }

        static long requestIndex;

        /// <summary>
        /// Sends a request through the HTTP pipeline to the remote service asynchronously.
        /// </summary>
        /// <param name="serviceRequest">The <see cref="ServiceRequest"/> to send to the service.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        /// <returns>A task that returns a <see cref="ServiceResponse"/> when complete.</returns>
        /// <exception cref="DatasyncInvalidOperationException">if content was expected but not received.</exception>
        internal async Task<ServiceResponse> SendAsync(ServiceRequest serviceRequest, CancellationToken cancellationToken = default)
        {
            var response = await SendForHttpResponseAsync(serviceRequest, cancellationToken);
            var serviceResponse = await ServiceResponse.CreateResponseAsync(response, cancellationToken).ConfigureAwait(false);
            response.Dispose();
            return serviceResponse;
        }

        internal async Task<HttpResponseMessage> SendForHttpResponseAsync(ServiceRequest serviceRequest, CancellationToken cancellationToken = default)
        {
            Arguments.IsNotNull(serviceRequest, nameof(serviceRequest));
            HttpRequestMessage request = serviceRequest.ToHttpRequestMessage();

            var reqIndex = requestIndex++;
            
            System.Diagnostics.Debug.WriteLine($"[{reqIndex}] ServiceHttpClient.SendAsync : ======================  REQUEST [{reqIndex}] ENTER ================== MainThread [{Xamarin.Essentials.MainThread.IsMainThread}]");
            System.Diagnostics.Debug.WriteLine($"[{reqIndex}] METHOD:           {request.Method}");
            if (request.Options.Any())
            {
                System.Diagnostics.Debug.WriteLine($"[{reqIndex}] OPTIONS:         ");
                foreach (var option in request.Options)
                    System.Diagnostics.Debug.WriteLine($"[{reqIndex}]\t\t {option.Key} : {option.Value} ");
            }
            if (client.DefaultRequestHeaders.Any())
            {
                System.Diagnostics.Debug.WriteLine($"[{reqIndex}] DEFAULT HEADERS:         ");
                foreach (var option in client.DefaultRequestHeaders)
                    System.Diagnostics.Debug.WriteLine($"[{reqIndex}]\t\t {option.Key} : {option.Value} ");
            }
            System.Diagnostics.Debug.WriteLine($"[{reqIndex}] BASE ADDRESS:     {client.BaseAddress}");
            System.Diagnostics.Debug.WriteLine($"[{reqIndex}] URI:              {request.RequestUri}");
            System.Diagnostics.Debug.WriteLine($"[{reqIndex}] CONTENT:          {request.Content}");
            System.Diagnostics.Debug.WriteLine($"[{reqIndex}] ServiceHttpClient.SendAsync : ======================  REQUEST [{reqIndex}] EXIT  ==================");
            
            HttpResponseMessage response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

            System.Diagnostics.Debug.WriteLine($"[{reqIndex}] ServiceHttpClient.SendAsync : ======================  RESPONSE [{reqIndex}] ENTER ==================");

            if (!response.IsSuccessStatusCode)
            {
                
                System.Diagnostics.Debug.WriteLine($"[{reqIndex}] INVALID STATUS CODE: {(int)response.StatusCode} ({response.StatusCode}) ");
                var contentx = await response.Content?.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[{reqIndex}] CONTENT: {contentx}");
                System.Diagnostics.Debug.WriteLine($"[{reqIndex}] ServiceHttpClient.SendAsync : ======================  RESPONSE [{reqIndex}] EXIT  ==================");
                
                throw await ThrowInvalidResponseAsync(request, response, cancellationToken);


            }

            if (serviceRequest.EnsureResponseContent)
            {
                if (!response.HasContent())
                {
                    
                    System.Diagnostics.Debug.WriteLine($"[{reqIndex}] NO CONTENT ");
                    System.Diagnostics.Debug.WriteLine($"[{reqIndex}] ServiceHttpClient.SendAsync : ======================  RESPONSE [{reqIndex}] EXIT  ==================");
                    
                    throw new DatasyncInvalidOperationException("The server did not provide a response with the expected content.", request, response);
                }

                System.Diagnostics.Debug.WriteLine($"[{reqIndex}] HasContent: {response.HasContent()}  IsCompressed: {response.HasContent()}");


                if (response.HasContent() && !response.IsCompressed())
                {
                    long? contentLength = response.Content.Headers.ContentLength;
                    if (contentLength == null || contentLength <= 0)
                    {
                        
                        System.Diagnostics.Debug.WriteLine($"[{reqIndex}] SERVER DID NOT PROVIDE EXPECTED CONTENT ");
                        var contentx = await response.Content?.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"[{reqIndex}] CONTENT: {contentx}");
                        System.Diagnostics.Debug.WriteLine($"[{reqIndex}] ServiceHttpClient.SendAsync : ======================  RESPONSE [{reqIndex}] EXIT  ==================");
                        
                        throw new DatasyncInvalidOperationException("The server did not provide a response with the expected content.", request, response);
                    }
                }
            }

            
            //System.Diagnostics.Debug.WriteLine($"[{reqIndex}] CONTENT: {await response.Content.ReadAsStringAsync()}");
            System.Diagnostics.Debug.WriteLine($"[{reqIndex}] ServiceHttpClient.SendAsync : VALID RESPONSE");
            System.Diagnostics.Debug.WriteLine($"[{reqIndex}] ServiceHttpClient.SendAsync : ======================  RESPONSE [{reqIndex}] EXIT  ==================");
            
            request.Dispose();
            return response;

        }

        /// <summary>
        /// throws an exception for an invalid response to a request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="response">The response.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        /// <returns>The exception to throw.</returns>
        private static async Task<Exception> ThrowInvalidResponseAsync(HttpRequestMessage request, HttpResponseMessage response, CancellationToken cancellationToken = default)
        {
            Arguments.IsNotNull(request, nameof(request));
            Arguments.IsNotNull(response, nameof(response));

            string responseContent = !response.HasContent() ? null : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string message = GetErrorMessageFromBody(responseContent) ?? $"The request could not be completed ({response.ReasonPhrase})";
            return new DatasyncInvalidOperationException($"{response?.StatusCode.ToString()??"no-status-code"}:{message}  Request:{request.RequestUri}", request, response);
        }

        /// <summary>
        /// When the server returns an error, the payload may contain the error message.  This
        /// method will convert whatever the payload is into the appropriate error message.
        /// </summary>
        /// <param name="content">The payload, or <c>null</c> if there is no payload.</param>
        /// <returns>The error message to use.</returns>
        private static string GetErrorMessageFromBody(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            // If it's not a JSON object, assume it's plain text
            if (content[0] != '{')
            {
                return content;
            }

            JToken body = ParseOrDefault(content);
            if (body?.Type == JTokenType.Object)
            {
                JToken error = body["error"];
                if (error?.Type == JTokenType.String)
                {
                    return error.ToString();
                }

                JToken description = body["description"];
                if (description?.Type == JTokenType.String)
                {
                    return description.ToString();
                }
            }

            return null;
        }

        /// <summary>
        /// Tries to parse a <see cref="JToken"/>; on failure, return <c>null</c>.
        /// </summary>
        /// <param name="content">The JSON content to attempt to parse.</param>
        /// <returns>The parsed <see cref="JToken"/> or <c>null</c>.</returns>
        private static JToken ParseOrDefault(string content)
        {
            try
            {
                return JToken.Parse(content);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Transform a list of <see cref="HttpMessageHandler"/> objects into a chain suitable for using
        /// as the pipeline of a <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="handlers">The list of <see cref="HttpMessageHandler"/> objects to transform</param>
        /// <returns>The chained <see cref="HttpMessageHandler"/></returns>
        protected HttpMessageHandler CreatePipeline(IEnumerable<HttpMessageHandler> handlers)
        {
            HttpMessageHandler pipeline = handlers.LastOrDefault() ?? DefaultHandlerFactory();
            if (pipeline is DelegatingHandler lastPolicy && lastPolicy.InnerHandler == null)
            {
                lastPolicy.InnerHandler = DefaultHandlerFactory();
                pipeline = lastPolicy;
            }

            // Wire handlers up in reverse order
            foreach (HttpMessageHandler handler in handlers.Reverse().Skip(1))
            {
                if (handler is DelegatingHandler policy)
                {
                    policy.InnerHandler = pipeline;
                    pipeline = policy;
                }
                else
                {
                    throw new ArgumentException("All message handlers except the last one must be 'DelegatingHandler'", nameof(handlers));
                }
            }
            return pipeline;
        }

        /// <summary>
        /// Returns a <see cref="HttpClientHandler"/> that supports automatic decompression.
        /// </summary>
        protected static HttpMessageHandler GetDefaultHttpClientHandler()
        {
            var handler = new HttpClientHandler();
            if (handler.SupportsAutomaticDecompression)
            {
                handler.AutomaticDecompression = System.Net.DecompressionMethods.GZip;
            }
            return handler;
        }

        #region IDisposable
        /// <summary>
        /// Implementation of the <see cref="IDisposable"/> pattern for derived classes to use
        /// </summary>
        /// <param name="disposing">True if calling from <see cref="Dispose()"/> or the finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                roothandler?.Dispose();
                roothandler = null;

                client?.Dispose();
                client = null;
            }
        }

        /// <summary>
        /// Implementation of the <see cref="IDisposable"/> pattern
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
