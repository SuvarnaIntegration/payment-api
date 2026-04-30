namespace PaymentAPI.Models
{

    public class CompanySettings
    {
        public int CompanyId { get; set; }
        public string CompanyCd { get; set; }
        public string CompanyName { get; set; }
        public string MobileNumber { get; set; }
        public string address { get; set; }
        public string faxNumber { get; set; }
        public string website { get; set; }
        public int CreateBy { get; set; }
        public string recordStatus { get; set; }

    }

    public class CompanySettingsResponse
    {
        public int CompanyId { get; set; }
        public string CompanyCd { get; set; }
        public string CompanyName { get; set; }
        public string MobileNumber { get; set; }
        public string address { get; set; }
        public string faxNumber { get; set; }
        public string website { get; set; }
        public string CreateBy { get; set; }
        public string ModifyBy { get; set; }
        public DateTime? CreateDt { get; set; }
        public DateTime? ModifyDt { get; set; }
        public string recordStatus { get; set; }
    }
    public class LocationSettings
    {
        public int LocationId { get; set; }
        public string LocationCd { get; set; }
        public string LocationName { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public int CreateBy { get; set; }
        public string recordStatus { get; set; }
    }

    public class LocationSettingsResponse
    {
        public int LocationId { get; set; }
        public string LocationCd { get; set; }
        public string LocationName { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public string CreateBy { get; set; }
        public string ModifyBy { get; set; }
        public DateTime? CreateDt { get; set; }
        public DateTime? ModifyDt { get; set; }
        public string recordStatus { get; set; }
    }

    public class ProviderSettings
    {
        public int ProviderId { get; set; }
        public string ProviderCd { get; set; }
        public string ProviderName { get; set; }
        public string Environment { get; set; }
        public string PosInitiateUrl { get; set; }
        public string PosStatusUrl { get; set; }
        public string PosCancelUrl { get; set; }
        public int CreateBy { get; set; }
        public string recordStatus { get; set; }
    }
    public class ProviderAccountStatusRequest
    {
        public int ProviderAccountId { get; set; }
        public string RecordStatus { get; set; }  // 'A' or 'I'
        public int ModifyBy { get; set; }
    }

    public class ApiStatusResponse
    {
        public string Code { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
    }

    public class ApiEdcStatusResponse
    {
        public string Status { get; set; }   // "true" / "false"
        public string Code { get; set; }     // "200" always
        public string Message { get; set; }
        public object Data { get; set; }
    }

    public class ApiListResponse<T>
    {
        public string Code { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public List<T> Data { get; set; }
    }

    public class ProviderSettingsResponse
    {
        public int ProviderId { get; set; }
        public string ProviderCd { get; set; }
        public string ProviderName { get; set; }
        public string Environment { get; set; }
        public string PosInitiateUrl { get; set; }
        public string PosStatusUrl { get; set; }
        public string PosCancelUrl { get; set; }
        public string CreateBy { get; set; }
        public string ModifyBy { get; set; }
        public DateTime? CreateDt { get; set; }
        public DateTime? ModifyDt { get; set; }
        public string recordStatus { get; set; }
    }

    public class ProviderAccountListResponse
    {
        public string ProviderWithAccount { get; set; }
        public int ProviderAccountId { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public int LocationId { get; set; }
        public string LocationName { get; set; }
        public int ProviderId { get; set; }
        public string ProviderName { get; set; }
    }

    public class EdcMachineRequest
    {
        public int PosEdcMachineId { get; set; }
        public string PosEdcMachineCd { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public int ProviderAccountId { get; set; }
        public string PosEdcSno { get; set; }
        public string PosEdcTerminalId { get; set; }
        public string StoreId { get; set; }
        public string ClientId { get; set; }
        public string MerchantPosCode { get; set; }
        public string PaytmMID { get; set; }
        public string PaytmMerchantKey { get; set; }
        public int CreateBy { get; set; }
        public string RecordStatus { get; set; }
    }


    public class PosEdcMachineSettings
    {
        public int posedcMachineId { get; set; }
        public string posEdcMachineCd { get; set; }
        public string posEdcMachineName { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public int LocationId { get; set; }
        public string LocationName { get; set; }
        public int ProviderId { get; set; }
        public string ProviderName { get; set; }
        public int ProviderAccountId { get; set; }
        public string ProviderAccountName { get; set; }
        public string PosEdcSno { get; set; }
        public string PosEdcTerminalId { get; set; }
        public int CreateBy { get; set; }
        public string recordStatus { get; set; }

    }

    public class PosEdcMachineResponse
    {
        public int PosedcMachineId { get; set; }
        public string? PosedcMachineCd { get; set; }
        public int? CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public int? LocationId { get; set; }
        public string? LocationName { get; set; }
        public int? ProviderId { get; set; }
        public string? ProviderName { get; set; }
        public int? ProviderAccountId { get; set; }
        public string? ProviderAccountName { get; set; }
        public string? PosedcMachineName { get; set; }
        public string? PosedcSno { get; set; }
        public string? PosedcTerminalId { get; set; }
        public DateTime? CreateDt { get; set; }
        public string? CreateBy { get; set; }
        public DateTime? ModifyDt { get; set; }
        public string? ModifyBy { get; set; }
        public string? RecordStatus { get; set; }
    }

    public class OperationResult
    {
        public string code { get; set; }
        public string status { get; set; }
        public string message { get; set; }
    }

    public class OperationMasterCode
    {
        public string code { get; set; }
        public string status { get; set; }
        public string message { get; set; }
        public string autocode { get; set; }
    }
    public class LoginResponse
    {
        public string code { get; set; }               // HTTP-like status code (e.g., 200, 401, 500)
        public string status { get; set; }            // Whether login was successful
        public string message { get; set; }         // Success or error message

        public int? UserId { get; set; }             // Nullable User ID from DB
        public string USERNAME { get; set; }         // User Name
        public string USERCODE { get; set; }         // User Code
        public string DISPLAYNAME { get; set; }      // User's display name
        public string HOSPITALCD { get; set; }       // Hospital code
        public string HOSPITALNAME { get; set; }     // Hospital name
        public string USERTYPE { get; set; }         // User type/role
        public string COSTCENTERCD { get; set; }     // Cost center code
        public int? COMPANYID { get; set; }     // Cost center code
    }

    public class LoginRequest
    {
        public string UserName { get; set; }         // User type/role
        public string Password { get; set; }     // Cost center code
    }

    public class PaylinkProviderAccount
    {
        // Basic Identifiers
        public int ProviderAccountId { get; set; }
        public string ProviderAccountCd { get; set; }
        public int ProviderId { get; set; }
        public string ProviderName { get; set; }
        public int LocationId { get; set; }
        public string LocationName { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public string AccountName { get; set; }

        // Worldline Credentials
        public string? WorldlineEncKey { get; set; }
        public string? WorldlineEncIv { get; set; }

        // Razorpay Credentials
        public string? RazorpayUsername { get; set; }
        public string? RazorpayAppKey { get; set; }

        // Pinelab Credentials
        public string? PinelabUserId { get; set; }
        public string? PinelabMerchantId { get; set; }
        public string? PinelabSecurityToken { get; set; }
        public string? PinelabMerchantStorePosCode { get; set; }

        // ICICI Credentials
        public string? IciciMid { get; set; }
        public string? IciciErpTranId { get; set; }
        public string? IciciErpClientId { get; set; }
        public string? IciciSourceId { get; set; }


        // 👇  ICICI PAYLINK 
        public string? IciciPaylinkMid { get; set; }
        public string? IciciPaylinkSecretKey { get; set; }

        // HDFC Credentials
        public string? HdfcMid { get; set; }
        public string? HdfcErpTranId { get; set; }
        public string? HdfcErpClientId { get; set; }
        public string? HdfcSourceId { get; set; }

        // MSwipe Credentials
        public string? MswipeUsername { get; set; }
        public string? MswipePassword { get; set; }
        public string? MswipeSaltKey { get; set; }
        public string? MswipeClientKey { get; set; }
        public string? MswipeClientSecret { get; set; }
        public string? MswipeClientCode { get; set; }

        // Paytm Credentials
        public string? PaytmMid { get; set; }
        public string? PaytmMerchantKey { get; set; }

        // PhonePe Credentials
        public string? PhonePeproviderId { get; set; }
        public string? PhonePeSaltKey { get; set; }
        public string? PhonePeMerchantId { get; set; }
        public string? PhonePeSaltIndex { get; set; }

        // Audit
        public int CreateBy { get; set; }
        public char RecordStatus { get; set; } = 'A'; // default Active
    }

    public class PaylinkProviderAccountResponse
    {
        public int PrvAcId { get; set; }
        public string PrvAccd { get; set; }
        public int ProviderId { get; set; }
        public string ProviderName { get; set; }
        public int LocationId { get; set; }
        public string LocationName { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public string AccountName { get; set; }
        public string WorldlineEncKey { get; set; }
        public string WorldlineEncIv { get; set; }
        public string RazorpayUsername { get; set; }
        public string RazorpayAppKey { get; set; }
        public string PinelabUserId { get; set; }
        public string PinelabMerchantId { get; set; }
        public string PinelabSecurityToken { get; set; }
        public string PinelabMerchantStorePosCode { get; set; }
        public string IciciMid { get; set; }
        public string IciciErpTranId { get; set; }
        public string IciciErpClientId { get; set; }
        public string IciciSourceId { get; set; }

        public string  icicipaylinkmid { get; set; }
        public string icicipaylinksecretkey { get; set; }
        public string HdfcMid { get; set; }
        public string HdfcErpTranId { get; set; }
        public string HdfcErpClientId { get; set; }
        public string HdfcSourceId { get; set; }
        public string MswipeUsername { get; set; }
        public string MswipePassword { get; set; }
        public string MswipeSaltKey { get; set; }
        public string MswipeClientKey { get; set; }
        public string MswipeClientSecret { get; set; }
        public string MswipeClientCode { get; set; }
        public string PaytmMid { get; set; }
        public string PaytmMerchantKey { get; set; }
        public string PhonePeproviderId { get; set; }
        public string PhonePeSaltKey { get; set; }
        public string PhonePeMerchantId { get; set; }
        public string PhonePeSaltIndex { get; set; }
        public string CreateBy { get; set; }
        public DateTime? CreateDt { get; set; }
        public string ModifyBy { get; set; }
        public DateTime? ModifyDt { get; set; }
        public char RecordStatus { get; set; }
    }

    public class PosEdcMachineDetails
    {
        public string? PosedcSno { get; set; }
        public string? PosedcTerminalId { get; set; }
        public string? ProviderName { get; set; }
        public string? RazorpayUsername { get; set; }
        public string? RazorpayAppKey { get; set; }
        public string? PosInitiateUrl { get; set; }
        public string? PosStatusUrl { get; set; }
        public string? PosCancelUrl { get; set; }
        public string? WorldlineEncKey { get; set; }
        public string? WorldlineEncIv { get; set; }
        public string? PinelabUserId { get; set; }
        public string? PinelabMerchantId { get; set; }
        public string? PinelabSecurityToken { get; set; }

        public string? PinelabStoreID { get; set; }
        public string? PinelabClientID { get; set; }
        public string? PinelabMerchantStorePosCode { get; set; }
        public string? IciciMid { get; set; }
        public string? IciciErpTranId { get; set; }
        public string? IciciErpClientId { get; set; }
        public string? IciciSourceId { get; set; }
        public string? HdfcMid { get; set; }
        public string? HdfcErpTranId { get; set; }
        public string? HdfcErpClientId { get; set; }
        public string? HdfcSourceId { get; set; }
        public string? MswipeUsername { get; set; }
        public string? MswipePassword { get; set; }
        public string? MswipeSaltKey { get; set; }
        public string? MswipeClientKey { get; set; }
        public string? MswipeClientSecret { get; set; }
        public string? MswipeClientCode { get; set; }
        public string? PaytmMid { get; set; }
        public string? PaytmMerchantKey { get; set; }
        public string? PhonePeproviderId { get; set; }
        public string? PhonePeSaltKey { get; set; }
        public string? PhonePeMerchantId { get; set; }
        public string? PhonePeSaltIndex { get; set; }
        public string? PhonePeStoreId { get; set; }
        public string? PhonePeStoreName { get; set; }
     
    }

    public class TokenResponse
    {
        public string authtoken { get; set; }
        public int code { get; set; }
        public string message { get; set; }
    }
    public class authtokenprop
    {
        public string UserName { get; set; }
        public string Password { get; set; }

    }

    public class PosActiveEdcMachineDetails
    {
        // POS / Machine Identifiers
        public string PosEdcSno { get; set; } = string.Empty;
        public string PosedcTerminalId { get; set; } = string.Empty;

        public string StoreId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string MerchantPosCode { get; set; } = string.Empty;

        // Provider Details
        public string ProviderName { get; set; } = string.Empty;

        // Razorpay
        public string RazorpayUsername { get; set; } = string.Empty;
        public string RazorpayAppKey { get; set; } = string.Empty;

        // POS URLs
        public string PosInitiateUrl { get; set; } = string.Empty;
        public string PosStatusUrl { get; set; } = string.Empty;
        public string PosCancelUrl { get; set; } = string.Empty;

        // Worldline
        public string WorldlineEncKey { get; set; } = string.Empty;
        public string WorldlineEncIv { get; set; } = string.Empty;

        // Pine Labs
        public string PinelabUserId { get; set; } = string.Empty;
        public string PinelabMerchantId { get; set; } = string.Empty;
        public string PinelabSecurityToken { get; set; } = string.Empty;
        public string PinelabMerchantStorePosCode { get; set; } = string.Empty;

        // ICICI
        public string IciciMid { get; set; } = string.Empty;
        public string IciciErpTranId { get; set; } = string.Empty;
        public string IciciErpClientId { get; set; } = string.Empty;
        public string IciciSourceId { get; set; } = string.Empty;

        // ICICI PAYLINK
        public string IciciPaylinkMid { get; set; } = string.Empty;
        public string IciciPaylinkSecretKey { get; set; } = string.Empty;

        // HDFC
        public string HdfcMid { get; set; } = string.Empty;
        public string HdfcErpTranId { get; set; } = string.Empty;
        public string HdfcErpClientId { get; set; } = string.Empty;
        public string HdfcSourceId { get; set; } = string.Empty;

        // Mswipe
        public string MswipeUsername { get; set; } = string.Empty;
        public string MswipePassword { get; set; } = string.Empty;
        public string MswipeSaltKey { get; set; } = string.Empty;
        public string MswipeClientKey { get; set; } = string.Empty;
        public string MswipeClientSecret { get; set; } = string.Empty;
        public string MswipeClientCode { get; set; } = string.Empty;

        // Paytm
        public string PaytmMid { get; set; } = string.Empty;
        public string PaytmMerchantKey { get; set; } = string.Empty;

        // PhonePe
        public string PhonePeproviderId { get; set; } = string.Empty;
        public string PhonePeSaltKey { get; set; } = string.Empty;
        public string PhonePeSaltIndex { get; set; } = string.Empty;
        public string PhonePeMerchantId { get; set; } = string.Empty;
    }

    public class ApiResponse
    {
        public bool Status { get; set; }
        public int Code { get; set; } = 200;
        public string Message { get; set; } = string.Empty;
        public object Data { get; set; } = null;
    }

    public static class ApiResponseHelper
    {
        public static ApiResponse Success(string message, object data = null)
        {
            return new ApiResponse
            {
                Status = true,
                Code = 200,
                Message = message,
                Data = data
            };
        }

        public static ApiResponse Error(string message)
        {
            return new ApiResponse
            {
                Status = false,
                Code = 200,
                Message = message,
                Data = null
            };
        }
    }

    public class PosEdcMachineTransactionLogDetails
    {
        public string? CompanyName { get; set; }
        public string? LocationName { get; set; }
        public string? ProviderName { get; set; }
        public string? AccountName { get; set; }
        public string? PosMachineSno { get; set; }
        public string? PosEdcTerminalId { get; set; }
        public string? TransactionNo { get; set; }
        public string? Amount { get; set; }
        public string? InitiateCustomerName { get; set; }
        public string? InitiateMobileNumber { get; set; }
        public string? TransactionType { get; set; }
        public string? PosTransactionRefNo { get; set; }
        public string? RequestName { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? TransactionLogId { get; set; }
    }

    public class PosEdcMachineTransactionLogJsonDtls
    {
        public int TransactionLogId { get; set; }
        public string JsonRequest { get; set; }
        public string JsonResponse { get; set; }
        public string StatusJsonRequest { get; set; }
        public string StatusJsonResponse { get; set; }
        public string CancelJsonRequest { get; set; }
        public string CancelJsonResponse { get; set; }

    }

}
