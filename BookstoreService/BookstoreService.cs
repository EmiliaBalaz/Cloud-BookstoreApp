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

namespace BookstoreService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class BookstoreService : StatefulService, IBookstore
    {
        private IReliableDictionary<string, Book>? _books;
        private IReliableDictionary<Guid, ReservedBook>? _reservedBooks;
        public BookstoreService(StatefulServiceContext context)
            : base(context)
        { }

        public object ViewBag { get; private set; }

        public async Task Commit(Guid transactionId)
        {
            _books = await StateManager.GetOrAddAsync<IReliableDictionary<string, Book>>("books");
            _reservedBooks = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedBook>>("reserved_books");

            using var tx = StateManager.CreateTransaction();

            var reservedBookResult = await _reservedBooks.TryGetValueAsync(tx, transactionId);
            if (!reservedBookResult.HasValue)
            {
                return; 
            }

            var reservedBook = reservedBookResult.Value;

            var bookResult = await _books.TryGetValueAsync(tx, reservedBook.BookId);
            if (!bookResult.HasValue)
            {
                return; 
            }

            var book = bookResult.Value;

            book.Quantity -= (int)reservedBook.Quantity;
            await _books.SetAsync(tx, reservedBook.BookId, book);
            await _reservedBooks.TryRemoveAsync(tx, transactionId);
            await tx.CommitAsync();
        }

        public async Task EnlistPurchase(Guid transactionId, string bookID, uint count)
        {
            _reservedBooks = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedBook>>("reserved_books");

            using var tx = StateManager.CreateTransaction();

            await _reservedBooks.SetAsync(tx, transactionId, new ReservedBook() { Quantity = count, BookId = bookID });

            await tx.CommitAsync();
        }

        public async Task<double> GetItemPrice(string bookID)
        {
            var stateManager = this.StateManager;
            _books = await stateManager.GetOrAddAsync<IReliableDictionary<string, Book>>("books");
            double bookPrice = 0;

            try
            {
                using (var tx = stateManager.CreateTransaction())
                {
                    var bookResult = await _books.TryGetValueAsync(tx, bookID);

                    if (!bookResult.HasValue)
                    {
                        throw new Exception($"Book with ID {bookID} doesn't exist!");
                    }

                    Book book = bookResult.Value;
                    bookPrice = book.Price;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while retrieving the book price.", ex);
            }

            return bookPrice;
        }

        public async Task<Dictionary<string, Book>> ListAvailableItems()
        {
            var stateManager = this.StateManager;
            var availableBooks = new Dictionary<string, Book>();
            var books = await stateManager.GetOrAddAsync<IReliableDictionary<string, Book>>("books");

            using (var tx = stateManager.CreateTransaction())
            {
                var enumerator = (await books.CreateEnumerableAsync(tx)).GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(CancellationToken.None))
                {
                    if (enumerator.Current.Value.Quantity > 0)
                    {
                        availableBooks.Add(enumerator.Current.Key, enumerator.Current.Value);
                    }
                }
            }

            return availableBooks;
        }

        public async Task<bool> Prepare(Guid transactionId)
        {
            _books = await StateManager.GetOrAddAsync<IReliableDictionary<string, Book>>("books");
            _reservedBooks = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedBook>>("reserved_books");

            using (var tx = StateManager.CreateTransaction()) //kreira se transakcija
            {
                var reservedBookResult = await _reservedBooks.TryGetValueAsync(tx, transactionId);  //prvo proverimo da li postoji rezervisana knjiga sa tim id-jem
                if (!reservedBookResult.HasValue)  //ako ne postoji, vracamo false i nastavljamo
                {
                    return false;
                }

                ReservedBook reservedBook = reservedBookResult.Value;

                var bookResult = await _books.TryGetValueAsync(tx, reservedBook.BookId);  //proverimo da li u Books postoji ova knjiga
                if (!bookResult.HasValue)
                {
                    return false;
                }

                Book book = bookResult.Value;

                return reservedBook.Quantity <= book.Quantity; // Proveramo da li je rezervisana kolièina dostupna
            }
        }

        public async Task Rollback(Guid transactionId)
        {
            _reservedBooks = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedBook>>("reserved_books");

            using (var tx = StateManager.CreateTransaction())
            {
                var removed = await _reservedBooks.TryRemoveAsync(tx, transactionId);
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

            //var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>("myDictionary");

            //while (true)
            //{
            //    cancellationToken.ThrowIfCancellationRequested();

            //    using (var tx = this.StateManager.CreateTransaction())
            //    {
            //        var result = await myDictionary.TryGetValueAsync(tx, "Counter");

            //        ServiceEventSource.Current.ServiceMessage(this.Context, "Current Counter Value: {0}",
            //            result.HasValue ? result.Value.ToString() : "Value does not exist.");

            //        await myDictionary.AddOrUpdateAsync(tx, "Counter", 0, (key, value) => ++value);

            //        // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
            //        // discarded, and nothing is saved to the secondary replicas.
            //        await tx.CommitAsync();
            //    }

            //    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

            // Simulacija inicijalizacije kolekcija u StateManager
            //_books = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Book>>("books");
            //_reservedBooks = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedBook>>("reserved_books");

            //// Kreirajte transakciju za pripremu podataka
            //using (var tx = this.StateManager.CreateTransaction())
            //{
            //    // Dodajte knjigu u _books
            //    await _books.AddOrUpdateAsync(tx, "book1", new Book { Title = "book1", Quantity = 10, Price = 10 }, (key, value) => value);

            //    // Dodajte rezervaciju u _reservedBooks
            //    var reservedBook = new ReservedBook { BookId = "book1", Quantity = 5 };
            //    Guid transactionId = Guid.NewGuid();
            //    await _reservedBooks.AddOrUpdateAsync(tx, transactionId, reservedBook, (key, value) => value);

            //    // Commit-ujte promene
            //    await tx.CommitAsync();
            //}

            //// Testirajte Prepare metodu
            //bool result = await Prepare(Guid.NewGuid());  // Pozivanje metode Prepare sa validnim transactionId
            //ServiceEventSource.Current.ServiceMessage(this.Context, "Prepare result: {0}", result ? "Success" : "Failure");

            //// Onda možete dodati još testova sa razlièitim uslovima
            //cancellationToken.ThrowIfCancellationRequested();
            //await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

            _books = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Book>>("books");
            _reservedBooks = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedBook>>("reserved_books");

            using var tx = this.StateManager.CreateTransaction();

            var enumerator = (await _books.CreateEnumerableAsync(tx)).GetAsyncEnumerator();

            if (!await enumerator.MoveNextAsync(cancellationToken))
            {
                Debug.WriteLine("---Uspesno inicijalizovani podaci!---");
                await _books.AddAsync(tx, "book1", new Book { Title = "Book 1", Quantity = 5, Price = 100 });
                await _books.AddAsync(tx, "book2", new Book { Title = "Book 2", Quantity = 1, Price = 50 });
                await _books.AddAsync(tx, "book3", new Book { Title = "Book 3", Quantity = 0, Price = 200 });
            }

            await _reservedBooks.ClearAsync();

            await tx.CommitAsync();
        }
    }
}

