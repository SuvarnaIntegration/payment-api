using System.Text.Json;

namespace PaymentAPI
{
    public class LogService
    {
        private readonly string _logBasePath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        private readonly int _retentionDays = 180; // keep only last 30 days

        public LogService()
        {
            if (!Directory.Exists(_logBasePath))
                Directory.CreateDirectory(_logBasePath);
        }

        public async Task WritePaymentLogAsync(string txnId, object request, object response)
        {
            try
            {
                string dateFolder = Path.Combine(_logBasePath, DateTime.UtcNow.ToString("yyyy-MM-dd"));
                if (!Directory.Exists(dateFolder))
                    Directory.CreateDirectory(dateFolder);

                string filePath = Path.Combine(dateFolder, txnId+"_payment_log.json");

                var logEntry = new
                {
                    TransactionId = txnId,
                    Request = request,
                    Response = response,
                    Timestamp = DateTime.UtcNow
                };

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string jsonData = JsonSerializer.Serialize(logEntry, jsonOptions) + Environment.NewLine;

                await File.AppendAllTextAsync(filePath, jsonData);

                // ✅ Cleanup old logs after writing
                CleanupOldLogs();
            }
            catch (Exception ex)
            {
                File.AppendAllText("error_log.txt", $"{DateTime.Now} - Failed to log: {ex.Message}\n");
            }
        }

        private void CleanupOldLogs()
        {
            try
            {
                var directories = Directory.GetDirectories(_logBasePath);

                foreach (var dir in directories)
                {
                    var dirName = Path.GetFileName(dir);

                    if (DateTime.TryParse(dirName, out var logDate))
                    {
                        if (logDate < DateTime.UtcNow.AddDays(-_retentionDays))
                        {
                            Directory.Delete(dir, true); // delete folder & files
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("error_log.txt", $"{DateTime.Now} - Failed cleanup: {ex.Message}\n");
            }
        }
    }
}
