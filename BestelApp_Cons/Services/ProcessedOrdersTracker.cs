using System.Collections.Concurrent;

namespace BestelApp_Cons.Services
{
    /// <summary>
    /// Tracker voor verwerkte orders
    /// Voorkomt dubbele verwerking van dezelfde order
    /// Gebruikt in-memory cache met tijdslimiet
    /// </summary>
    public class ProcessedOrdersTracker
    {
        // Thread-safe dictionary voor verwerkte order IDs
        private readonly ConcurrentDictionary<string, ProcessedOrderInfo> _processedOrders;
        
        // Hoe lang een order ID in cache blijft (standaard 24 uur)
        private readonly TimeSpan _cacheDuration;

        public ProcessedOrdersTracker(TimeSpan? cacheDuration = null)
        {
            _processedOrders = new ConcurrentDictionary<string, ProcessedOrderInfo>();
            _cacheDuration = cacheDuration ?? TimeSpan.FromHours(24);

            // Start cleanup task
            _ = StartCleanupTask();
        }

        /// <summary>
        /// Check of order al verwerkt is
        /// </summary>
        /// <param name="orderId">Unieke Order ID</param>
        /// <returns>True als al verwerkt, anders False</returns>
        public bool IsOrderAlreadyProcessed(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return false;
            }

            // Check of order ID in cache zit
            if (_processedOrders.TryGetValue(orderId, out var info))
            {
                // Check of nog niet verlopen
                if (DateTime.UtcNow - info.ProcessedAt < _cacheDuration)
                {
                    Console.WriteLine($"⚠️  Order {orderId} al verwerkt op {info.ProcessedAt:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"    Aantal keer gezien: {info.ProcessCount}");
                    
                    // Increment counter
                    info.ProcessCount++;
                    info.LastSeenAt = DateTime.UtcNow;
                    
                    return true;
                }
                else
                {
                    // Verlopen, verwijder uit cache
                    _processedOrders.TryRemove(orderId, out _);
                    Console.WriteLine($"ℹ️  Order {orderId} cache verlopen, opnieuw verwerken toegestaan");
                }
            }

            return false;
        }

        /// <summary>
        /// Markeer order als verwerkt
        /// </summary>
        /// <param name="orderId">Unieke Order ID</param>
        public void MarkAsProcessed(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return;
            }

            var info = new ProcessedOrderInfo
            {
                OrderId = orderId,
                ProcessedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                ProcessCount = 1
            };

            _processedOrders.AddOrUpdate(orderId, info, (key, existing) =>
            {
                existing.LastSeenAt = DateTime.UtcNow;
                existing.ProcessCount++;
                return existing;
            });

            Console.WriteLine($"✓ Order {orderId} gemarkeerd als verwerkt");
        }

        /// <summary>
        /// Haal statistieken op
        /// </summary>
        public ProcessedOrdersStats GetStats()
        {
            var now = DateTime.UtcNow;
            var validOrders = _processedOrders.Values
                .Where(o => now - o.ProcessedAt < _cacheDuration)
                .ToList();

            return new ProcessedOrdersStats
            {
                TotalProcessed = validOrders.Count,
                DuplicatesDetected = validOrders.Sum(o => o.ProcessCount - 1),
                OldestEntry = validOrders.Any() ? validOrders.Min(o => o.ProcessedAt) : (DateTime?)null,
                NewestEntry = validOrders.Any() ? validOrders.Max(o => o.ProcessedAt) : (DateTime?)null
            };
        }

        /// <summary>
        /// Cleanup task om verlopen entries te verwijderen
        /// </summary>
        private async Task StartCleanupTask()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromHours(1)); // Elke uur cleanup

                var now = DateTime.UtcNow;
                var expiredKeys = _processedOrders
                    .Where(kvp => now - kvp.Value.ProcessedAt > _cacheDuration)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _processedOrders.TryRemove(key, out _);
                }

                if (expiredKeys.Count > 0)
                {
                    Console.WriteLine($"🧹 Cleanup: {expiredKeys.Count} verlopen orders verwijderd uit cache");
                }
            }
        }
    }

    /// <summary>
    /// Info over een verwerkte order
    /// </summary>
    public class ProcessedOrderInfo
    {
        public string OrderId { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
        public DateTime LastSeenAt { get; set; }
        public int ProcessCount { get; set; }
    }

    /// <summary>
    /// Statistieken over verwerkte orders
    /// </summary>
    public class ProcessedOrdersStats
    {
        public int TotalProcessed { get; set; }
        public int DuplicatesDetected { get; set; }
        public DateTime? OldestEntry { get; set; }
        public DateTime? NewestEntry { get; set; }

        public override string ToString()
        {
            return $"Processed: {TotalProcessed}, Duplicates: {DuplicatesDetected}";
        }
    }
}
