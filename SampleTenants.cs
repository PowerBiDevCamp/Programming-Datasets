using ProgrammingDatasets.Models;

namespace ProgrammingDatasets {

  public class SampleTenants {
    
    public static PowerBiTenant Wingtip = new PowerBiTenant { 
      Name = "Wingtip", 
      DatabaseServer = "devcamp.database.windows.net", 
      DatabaseName = "WingtipSales", 
      DatabaseUserName = "CptStudent", 
      DatabaseUserPassword = "pass@word1" 
    };
    
    public static PowerBiTenant Contoso = new PowerBiTenant { 
      Name = "Contoso", 
      DatabaseServer = "devcamp.database.windows.net", 
      DatabaseName = "ContosoSales", 
      DatabaseUserName = "CptStudent", 
      DatabaseUserPassword = "pass@word1" };

    public static PowerBiTenant AcmeCorp = new PowerBiTenant { 
      Name = "Acme Corp", 
      DatabaseServer = "devcamp.database.windows.net", 
      DatabaseName = "AcmeCorpSales", 
      DatabaseUserName = "CptStudent", 
      DatabaseUserPassword = "pass@word1" 
    };

    public static PowerBiTenant MegaCorp = new PowerBiTenant { 
      Name = "Mega Corp", 
      DatabaseServer = "devcamp.database.windows.net", 
      DatabaseName = "MegaCorpSales", 
      DatabaseUserName = "CptStudent", 
      DatabaseUserPassword = "pass@word1" 
    };

  }

}
