using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Interfaces;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Models;

namespace ValidationService
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class ValidationService : StatelessService, IValidation
    {
        private readonly ITransactionCoordinator _transactionCoordinatorService;
        public ValidationService(StatelessServiceContext context)
            : base(context)
        {
            var serviceProxyFactory = new ServiceProxyFactory((callbackClient) =>
            {
                return new FabricTransportServiceRemotingClientFactory(
                    new FabricTransportRemotingSettings
                    {
                        ExceptionDeserializationTechnique = FabricTransportRemotingSettings.ExceptionDeserialization.Default
                    },
                    callbackClient);
            });

            var serviceUri = new Uri("fabric:/Zadatak/TransactionCoordinatorService");
            _transactionCoordinatorService = serviceProxyFactory.CreateServiceProxy<ITransactionCoordinator>(serviceUri, new ServicePartitionKey(0));
        }

        public async Task<bool> ValidateBookAsync(string title, int quantity)
        {
            if(!string.IsNullOrEmpty(title) && quantity > 0)
            {
                await _transactionCoordinatorService.StartTransaction(title, quantity);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new List<ServiceInstanceListener>
            {
                new ServiceInstanceListener(serviceContext =>
                    new FabricTransportServiceRemotingListener(
                        serviceContext,
                        this,
                        new FabricTransportRemotingListenerSettings
                            {
                                ExceptionSerializationTechnique = FabricTransportRemotingListenerSettings.ExceptionSerialization.Default,
                            }),
                        "ServiceEndpointV2")
            };
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            long iterations = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ServiceEventSource.Current.ServiceMessage(this.Context, "Working-{0}", ++iterations);

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
