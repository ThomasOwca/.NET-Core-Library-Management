using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibraryData;
using LibraryData.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryServices
{
    public class CheckoutServices : ICheckout
    {
        private LibraryContext _context;
  
        public CheckoutServices(LibraryContext context)
        {
            _context = context;
        }
        
        public void Add(Checkout newCheckout)
        {
            _context.Add(newCheckout);

            // In another scenario, you wouldn't want to call this
            // SaveChanges() method if you were selecting multiple items
            // at once. Something to keep in mind.
            _context.SaveChanges();
        }

        public void CheckInItem(int assetId)
        {
            var now = DateTime.Now;

            var item = _context.LibraryAssets
                .FirstOrDefault(a => a.Id == assetId);

            // remove any existing checkouts
            RemoveExistingCheckouts(assetId);

            // close any existing checkout history
            CloseExistingCheckoutHistory(assetId, now);

            // look for existing holds on the item.
            var currentHolds = _context.Holds
                .Include(h => h.LibraryAsset)
                .Include(h => h.LibraryCard)
                .Where(h => h.LibraryAsset.Id == assetId);

            // if there are holds, checkout the item to the
            // libarycard with the earliest hold.
            if (currentHolds.Any())
            {
                CheckoutToEarliestHold(assetId, currentHolds);
                return;
            }

            // otherwise, update the item status to available
            UpdateAssetStatus(assetId, "Available");

            _context.SaveChanges();
        }

        private void CheckoutToEarliestHold(int assetId, IQueryable<Hold> currentHolds)
        {
            // COME BACK AND STUDY THIS METHOD IN-DEPTH
            var earliestHold = currentHolds
                .OrderBy(holds => holds.HoldPlaced)
                .FirstOrDefault();

            var card = earliestHold.LibraryCard;

            _context.Remove(earliestHold);
            _context.SaveChanges();

            CheckOutItem(assetId, card.Id);
        }

        public void CheckOutToFirstReserve(int assetId, int firstHoldLibraryCardId)
        {
            if (IsCheckedOut(assetId))
            {
                return;
                // Add logic here to handle feedback to the user.
            }

            var item = _context.LibraryAssets
                .FirstOrDefault(a => a.Id == assetId);

            UpdateAssetStatus(assetId, "Checked Out");

            var libraryCard = _context.LibraryCards
                .Include(card => card.Checkouts)
                .FirstOrDefault(card => card.Id == firstHoldLibraryCardId);

            var now = DateTime.Now;

            var checkout = new Checkout
            {
                LibraryAsset = item,
                LibraryCard = libraryCard,
                Since = now,
                Until = GetDefaultCheckoutTime(now)
            };

            _context.Add(checkout);

            var checkoutHistory = new CheckoutHistory
            {
                CheckedOut = now,
                LibraryAsset = item,
                LibraryCard = libraryCard
            };

            _context.Add(checkoutHistory);

            var earliestHold = _context.Holds
                .OrderBy(h => h.HoldPlaced)
                .FirstOrDefault(h => h.LibraryCard.Id == firstHoldLibraryCardId);

            _context.Remove(earliestHold);

            _context.SaveChanges();
        }

        public void CheckOutItem(int assetId, int libraryCardId)
        {
            if (IsCheckedOut(assetId))
            {
                return;
                // Add logic here to handle feedback to the user.
            }

            var item = _context.LibraryAssets
                .FirstOrDefault(a => a.Id == assetId);

            UpdateAssetStatus(assetId, "Checked Out");

            var libraryCard = _context.LibraryCards
                .Include(card => card.Checkouts)
                .FirstOrDefault(card => card.Id == libraryCardId);

            var now = DateTime.Now;

            var checkout = new Checkout
            {
                LibraryAsset = item,
                LibraryCard = libraryCard,
                Since = now,
                Until = GetDefaultCheckoutTime(now)
            };

            _context.Add(checkout);

            var checkoutHistory = new CheckoutHistory
            {
                CheckedOut = now,
                LibraryAsset = item,
                LibraryCard = libraryCard
            };

            _context.Add(checkoutHistory);
            _context.SaveChanges();
        }

        private DateTime GetDefaultCheckoutTime(DateTime now)
        {
            return now.AddDays(30);
        }

        public bool IsCheckedOut(int assetId)
        {
            var isCheckedOut = _context.Checkouts
                .Where(co => co.LibraryAsset.Id == assetId)
                .Any();

            return isCheckedOut;
        }

        public IEnumerable<Checkout> GetAll()
        {
            return _context.Checkouts;
        }

        public Checkout GetById(int checkoutId)
        {
            // Returns first or default returned by this LINQ operation where the IDs match.
            return GetAll()
                .FirstOrDefault(checkout => checkout.Id == checkoutId);
        }

        public IEnumerable<CheckoutHistory> GetCheckoutHistories(int id)
        {
            // This LINQ Operation basically returns the equivalent to the following
            // SQL Query Command, assuming that the passed in argument is 4:

            /*
             * SELECT * FROM CheckoutHistories
             * INNER JOIN LibraryAssets ON CheckoutHistories.LibraryAssetId = LibraryAssets.Id
             * INNER JOIN LibraryCards ON CheckoutHistories.LibraryCardId = LibraryCards.Id
             * WHERE LibraryAssets.Id = 4;
             */

            return _context.CheckoutHistories
                .Include(h => h.LibraryAsset)
                .Include(h => h.LibraryCard)
                .Where(h => h.LibraryAsset.Id == id);
        }

        public IEnumerable<Hold> GetCurrentHolds(int id)
        {
            // This LINQ Operation basically returns the equivalent to the following
            // SQL Query Command, assuming that the passed in argument is 4:

            /*
             * SELECT * FROM Holds
             * INNER JOIN LibraryAssets ON Holds.LibraryAssetId = LibraryAssets.Id
             * WHERE LibraryAssetId = 4;
             */
            return _context.Holds
                .Include(h => h.LibraryAsset)
                .Include (h => h.LibraryCard)
                .Where(h => h.LibraryAsset.Id == id);
        }

        public Checkout GetLastestCheckout(int assetId)
        {
            // This LINQ is basically equivalent to the following in SQL:

            /*
             * SELECT * FROM Checkouts
             * WHERE LibraryAssetId = assetId
             * ORDER BY Since DESC;
             */
            return _context.Checkouts
                .Where(c => c.LibraryAsset.Id == assetId)
                .OrderByDescending(c => c.Since)
                .FirstOrDefault();
        }

        public Hold GetLastestHold(int assetId)
        {
            return _context.Holds
                .Include(c => c.LibraryAsset)
                .Include(c => c.LibraryCard)
                .Where(c => c.LibraryAsset.Id == assetId)
                .OrderBy(c => c.HoldPlaced)
                .FirstOrDefault();
        }

        public void MarkFound(int assetId)
        {
            var now = DateTime.Now;

            UpdateAssetStatus(assetId, "Available");
            RemoveExistingCheckouts(assetId);
            CloseExistingCheckoutHistory(assetId, now);
            _context.SaveChanges();
        }

        private void UpdateAssetStatus(int assetId, string statusName)
        {
            var item = _context.LibraryAssets
                .FirstOrDefault(a => a.Id == assetId);

            // Calling Update() tells EF Core to start tracking this item.
            _context.Update(item);

            item.Status = _context.Statuses
                .FirstOrDefault(status => status.Name == statusName);
        }

        private void CloseExistingCheckoutHistory(int assetId, DateTime now)
        {
            // Close any existing checkout history
            var history = _context.CheckoutHistories
                .FirstOrDefault(h => h.LibraryAsset.Id == assetId
                    && h.CheckedIn == null);

            if (history != null)
            {
                _context.Update(history);
                history.CheckedIn = now;
            }
        }

        private void RemoveExistingCheckouts(int assetId)
        {
            // Remove any existing checkouts on the item
            var checkout = _context.Checkouts
                .FirstOrDefault(co => co.LibraryAsset.Id == assetId);

            if (checkout != null)
            {
                // Removes the returned Checkout object from the DB Table.
                // Once SaveChanges() method is called, it will be deleted.
                _context.Remove(checkout);
            }
        }

        public void MarkLost(int assetId)
        {
            UpdateAssetStatus(assetId, "Lost");
            _context.SaveChanges();
        }

        public void PlaceHold(int assetId, int libraryCardId)
        {
            var now = DateTime.Now;

            var asset = _context.LibraryAssets
                .Include(a => a.Status)
                .FirstOrDefault(a => a.Id == assetId);

            var card = _context.LibraryCards
                .FirstOrDefault(c => c.Id == libraryCardId);

            // If user input for library card doesn't exist. Return.
            if (card == null)
            {
                return;
            }

            if (asset.Status.Name == "Available")
            {
                UpdateAssetStatus(assetId, "On Hold");
            }

            var hold = new Hold
            {
                HoldPlaced = now,
                LibraryAsset = asset,
                LibraryCard = card
            };

            _context.Add(hold);
            _context.SaveChanges();
        }

        public string GetCurrentHoldPatronName(int holdId)
        {
            var hold = _context.Holds
                .Include(h => h.LibraryAsset)
                .Include(h => h.LibraryCard)
                .FirstOrDefault(h => h.Id == holdId);

            var cardId = hold?.LibraryCard.Id;

            var patron = _context.Patrons
                .Include(p => p.LibraryCard)
                .FirstOrDefault(p => p.LibraryCard.Id == cardId);

            return patron?.FirstName + " " + patron?.LastName;
        }

        public DateTime GetCurrentHoldPlaced(int holdId)
        {
            return 
                _context.Holds
                .Include(h => h.LibraryAsset)
                .Include(h => h.LibraryCard)
                .FirstOrDefault(h => h.Id == holdId)
                .HoldPlaced;
        }

        public string GetCurrentCheckoutPatron(int assetId)
        {
            var checkout = GetCheckoutByAssetId(assetId);

            if (GetCheckoutByAssetId(assetId) == null)
            {
                return "";
            }

            var cardId = checkout.LibraryCard.Id;

            var patron = _context.Patrons
                //.Include(p => p.LibraryCard)
                .FirstOrDefault(p => p.LibraryCard.Id == cardId);

            return patron.FirstName + " " + patron.LastName;
        }

        private Checkout GetCheckoutByAssetId(int assetId)
        {
            var checkout = _context.Checkouts
                .Include(co => co.LibraryAsset)
                .Include(co => co.LibraryCard)
                .FirstOrDefault(co => co.LibraryAsset.Id == assetId);

            return checkout;
        }
    }
}
