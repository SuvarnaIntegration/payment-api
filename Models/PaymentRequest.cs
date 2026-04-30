namespace PaymentAPI.Models
{
    public class PaymentRequest
    {
        public string Provider { get; set; }
        public string Locid { get; set; }
        public string Locname { get; set; }
        public string Companyname { get; set; }
        public string Tid { get; set; }
        public string refernceId { get; set; }
        public string Patientid { get; set; }
        public string Patientname { get; set; }
        public string Invoiceno { get; set; }
        public string Mobileno { get; set; }
        public string Amount { get; set; }
        public string Currency { get; set; }
        public string Txntype { get; set; }
        public string Remarks { get; set; }
        public string Timestamp { get; set; }
    }

    public class PosPaymentRequest
    {
        public string provider { get; set; } = string.Empty;
        public string edcsNo { get; set; } = string.Empty;
        public string customerName { get; set; } = string.Empty;
        public string mobileNumber { get; set; } = string.Empty;
        public string patientId { get; set; } = string.Empty;
        public string transactionNo { get; set; } = string.Empty;
        public string refernceId { get; set; } = string.Empty;
        public string amount { get; set; } = string.Empty;
        public string currency { get; set; } = string.Empty;
        public string txntype { get; set; } = string.Empty;
        public string createby { get; set; } = string.Empty;
    }


    public class PaymentRequestResponse
    {
        public int code { get; set; }
        public string status { get; set; }
        public string message { get; set; }
        public string transactionId { get; set; }
        public string refernceId { get; set; }
        public string Orginalcode { get; set; }
        public string OrginalMessage { get; set; }
        public string RRN { get; set; }
        public string cardNo { get; set; }
        public string upi { get; set; }
        public string paymentMode { get; set; }

        public string QrCodeurl { get; set; }
        public string PaymentUrl { get; set; }
        // 🔹 Keep the original JSON response
        public string originalResponse { get; set; }
    }

}
