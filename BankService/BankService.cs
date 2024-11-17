using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Interfaces;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Models;

namespace BankService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class BankService : StatefulService, IBank
    {
        private IReliableDictionary<string, Client>? _clients;
        private IReliableDictionary<Guid, ReservedFund>? _reservedFunds;
        public BankService(StatefulServiceContext context)
            : base(context)
        { }

        public Task Commit(Guid transactionId)
        {
            throw new NotImplementedException();
        }

        public Task EnlistMoneyTransfer(Guid transactionId, string userID, double amount)
        {
            throw new NotImplementedException();
        }

        public async Task<Dictionary<string, Client>> ListClients()
        {
            var clients = new Dictionary<string, Client>();
            var clientsDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, Client>>("clients");

            using (var tx = StateManager.CreateTransaction())
            {
                var enumerator = (await clientsDictionary.CreateEnumerableAsync(tx)).GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(CancellationToken.None))
                {
                    clients.Add(enumerator.Current.Key, enumerator.Current.Value);
                }
            }

            return clients;
        }

        public async Task<bool> Prepare(Guid transactionId)
        {
            _clients = await StateManager.GetOrAddAsync<IReliableDictionary<string, Client>>("clients");
            _reservedFunds = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedFund>>("reserved_funds");

            using (var tx = StateManager.CreateTransaction())
            {
                
                var reservedFundResult = await _reservedFunds.TryGetValueAsync(tx, transactionId); //da li postoje rezervisana sredstva za dati id
                if (!reservedFundResult.HasValue)
                {
                    return false;
                }

                ReservedFund reservedFund = reservedFundResult.Value;

                var clientResult = await _clients.TryGetValueAsync(tx, reservedFund.ClientId); //da li klijent sa datim id-jem postoji
                if (!clientResult.HasValue)
                {
                    return false;
                }

                Client client = clientResult.Value;

                return reservedFund.Amount <= client.Balance; //proveri da li klijent ima dovoljno novca
            }
        }

        public async Task Rollback(Guid transactionId)
        {
            _reservedFunds = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedFund>>("reserved_funds");

            using (var tx = StateManager.CreateTransaction())
            {
                var removed = await _reservedFunds.TryRemoveAsync(tx, transactionId);
                if (removed.HasValue)
                {
                    await tx.CommitAsync();
                }
                else
                {
                    await tx.CommitAsync();
                }
            }
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

            _clients = await StateManager.GetOrAddAsync<IReliableDictionary<string, Client>>("clients");
            _reservedFunds = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedFund>>("reserved_funds");

            using var tx = StateManager.CreateTransaction();

            var enumerator = (await _clients.CreateEnumerableAsync(tx)).GetAsyncEnumerator();

            if (!await enumerator.MoveNextAsync(cancellationToken))
            {
                Debug.WriteLine("---Uspesno inicijalizovani podaci!---");
                await _clients.AddAsync(tx, "client1", new Client { ClientName = "Emilija", Balance = 20000 });
                await _clients.AddAsync(tx, "client2", new Client { ClientName = "Dijana", Balance = 1000 });
            }
            await _reservedFunds.ClearAsync();
            await tx.CommitAsync();
        }
    }
}
