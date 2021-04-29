namespace ProgrammingDatasets {

  class AppSettings {

    // metadata from public Azure AD application
    public const string ApplicationId = "";
    public const string RedirectUri = "http://localhost";

    // metadata from confidential Azure AD application for service principal
    public const string tenantId = "";
    public const string confidentialApplicationId = "";
    public const string confidentialApplicationSecret = "";
    public const string tenantSpecificAuthority = "https://login.microsoftonline.com/" + tenantId;
    public const string servicePrincipalObjectId = "";
    public const string adminUser = "YourAccount@YOUR_ORG.onMicrosoft.com";

    public const string dedicatedCapacityId = "";

    // connection string Tabular Object Model 
    public const string WorkspaceConnection = "powerbi://api.powerbi.com/v1.0/myorg/SomePremiumWorkspace";

    // info required to patch SQL credentlas
    public const string sqlUserName = "";
    public const string sqlUserPassword = "";

    // info required to u[pdate query to redirect to ADLS
    public const string adlsFilePath = "https://storeaccount1.blob.core.windows.net/exceldata/";
    public const string adlsBlobAccount = "https://storeaccount1.blob.core.windows.net/";
    public const string adlsBlobContainer = "exceldata/";
    public const string adlsFileName = "SalesDataProd2.xlsx";

    // key required to configure credentials
    public const string adlsStorageKey = "";

  }
}