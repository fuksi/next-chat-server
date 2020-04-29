using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.ServiceFabric.Data;
using Serilog;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace NextChat.ChatApi
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance. 
    /// </summary>
    internal sealed class ChatApi : StatelessService
    {
        public ChatApi(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            var serviceEndPointName = "ServiceEndpoint";
#if DEBUG
            serviceEndPointName = "UnsecuredServiceEndpoint";
#endif
            return new ServiceInstanceListener[]
            {
                new ServiceInstanceListener(serviceContext =>
                    new KestrelCommunicationListener(serviceContext, serviceEndPointName, (url, listener) =>
                    {
                        ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting Kestrel on {url}");

                        return new WebHostBuilder()
#if DEBUG
                                    .UseKestrel()
#else
                                    .UseKestrel(opt => {
                                        int port = serviceContext.CodePackageActivationContext.GetEndpoint(serviceEndPointName).Port;
                                        opt.Listen(IPAddress.IPv6Any, port, listenOptions =>
                                        {
                                            listenOptions.UseHttps(FindMatchingCertificateBySubject("nextchat.me"));
                                        });
                                    })
#endif
                                    .ConfigureServices(
                                        services => services
                                            .AddSingleton<StatelessServiceContext>(serviceContext))
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseStartup<Startup>()
                                    .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
                                    .UseUrls(url)
                                    .UseSerilog()
                                    .Build();
                    }))
            };
        }

        private X509Certificate2 FindMatchingCertificateBySubject(string subjectCommonName)
        {
            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);
            var certCollection = store.Certificates;
            var matchingCerts = new X509Certificate2Collection();

            foreach (var enumeratedCert in certCollection)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(subjectCommonName, enumeratedCert.GetNameInfo(X509NameType.SimpleName, forIssuer: false))
                  && DateTime.Now < enumeratedCert.NotAfter
                  && DateTime.Now >= enumeratedCert.NotBefore)
                {
                    matchingCerts.Add(enumeratedCert);
                }
            }

            if (matchingCerts.Count == 0)
            {
                throw new Exception($"Could not find a match for a certificate with subject 'CN={subjectCommonName}'.");
            }

            return matchingCerts[0];
        }
    }
}
