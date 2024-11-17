using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Interfaces;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace TransactionCoordinatorService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class TransactionCoordinatorService : StatefulService, ITransactionCoordinator
    {
        private readonly IBookstore _bookstoreService;
        private readonly IBank _bankService;
        public TransactionCoordinatorService(StatefulServiceContext context)
            : base(context)
        {
            var serviceProxyFactoryBookstore = new ServiceProxyFactory((callbackClient) =>
            {
                return new FabricTransportServiceRemotingClientFactory(
                    new FabricTransportRemotingSettings
                    {
                        ExceptionDeserializationTechnique = FabricTransportRemotingSettings.ExceptionDeserialization.Default
                    },
                    callbackClient);
            });

            var serviceUriBookStore = new Uri("fabric:/Zadatak/BookstoreService");
            _bookstoreService = serviceProxyFactoryBookstore.CreateServiceProxy<IBookstore>(serviceUriBookStore, new ServicePartitionKey(0));

            var serviceProxyFactoryBank = new ServiceProxyFactory((callbackClient) =>
            {
                return new FabricTransportServiceRemotingClientFactory(
                    new FabricTransportRemotingSettings
                    {
                        ExceptionDeserializationTechnique = FabricTransportRemotingSettings.ExceptionDeserialization.Default
                    },
                    callbackClient);
            });

            var serviceUriBank = new Uri("fabric:/Zadatak/BankService");
            _bankService = serviceProxyFactoryBank.CreateServiceProxy<IBank>(serviceUriBank, new ServicePartitionKey(0));
        }

        public async Task StartTransaction(string title, int quantity, string client)
        {
            Guid transactionId = Guid.NewGuid(); // Generate a unique transaction ID

            var bookID = await GetBookIdByTitle(title);
            if (string.IsNullOrEmpty(bookID))
            {
                throw new InvalidOperationException("Book not found.");
            }

            double price = await _bookstoreService.GetItemPrice(bookID);

            var clientID = await GetClientIdByName(client);
            if (string.IsNullOrEmpty(clientID))
            {
                throw new InvalidOperationException("Client not found.");
            }

            double amount = quantity * price;

            try
            {
                await _bookstoreService.EnlistPurchase(transactionId, bookID, (uint)quantity); 
                //await _bankService.EnlistMoneyTransfer(transactionId, clientID, amount); 

                bool isPreparedBookstore = await _bookstoreService.Prepare(transactionId); 
               // bool isPreparedBank = await _bankService.Prepare(transactionId); 

                if (isPreparedBookstore) //&& ispreparedBank
                {
                    await _bookstoreService.Commit(transactionId); 
                    //await _bankService.Commit(transactionId); 
                }
                else
                {
                    await _bookstoreService.Rollback(transactionId); 
                    //await _bankService.Rollback(transactionId);
                }
            }
            catch (Exception ex)
            {
                await _bookstoreService.Rollback(transactionId); 
                //await _bankService.Rollback(transactionId);
                throw new Exception(ex.Message); 
            }
        }

        private async Task<string> GetBookIdByTitle(string title)
        {
            var availableBooks = await _bookstoreService.ListAvailableItems();
            var book = availableBooks.FirstOrDefault(b =>
                b.Value.Title.Equals(title, StringComparison.OrdinalIgnoreCase));

            return book.Key != null ? book.Key : null;
        }

        private async Task<string> GetClientIdByName(string clientName)
        {
            var clients = await _bankService.ListClients();
            var client = clients.FirstOrDefault(c =>
                c.Value.ClientName.Equals(clientName, StringComparison.OrdinalIgnoreCase));

            return client.Key != null ? client.Key : null;
        }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new List<ServiceReplicaListener>
            {
                new ServiceReplicaListener(serviceContext =>
                    new FabricTransportServiceRemotingListener(
                        serviceContext,
                        this, new FabricTransportRemotingListenerSettings
                            {
                                ExceptionSerializationTechnique = FabricTransportRemotingListenerSettings.ExceptionSerialization.Default,
                            })
                    )
            };
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>("myDictionary");

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var tx = this.StateManager.CreateTransaction())
                {
                    var result = await myDictionary.TryGetValueAsync(tx, "Counter");

                    ServiceEventSource.Current.ServiceMessage(this.Context, "Current Counter Value: {0}",
                        result.HasValue ? result.Value.ToString() : "Value does not exist.");

                    await myDictionary.AddOrUpdateAsync(tx, "Counter", 0, (key, value) => ++value);

                    // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
                    // discarded, and nothing is saved to the secondary replicas.
                    await tx.CommitAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
