namespace SmartERP.Application.Common;

/// <summary>Role names plus the role groups used by [Authorize(Roles = ...)] on controllers.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Sales = "Sales";
    public const string Purchase = "Purchase";
    public const string Inventory = "Inventory";
    public const string Accountant = "Accountant";

    public static readonly string[] All = [Admin, Manager, Sales, Purchase, Inventory, Accountant];

    public const string CanSell = $"{Admin},{Manager},{Sales}";
    public const string CanBuy = $"{Admin},{Manager},{Purchase}";
    public const string CanManageStock = $"{Admin},{Manager},{Inventory},{Purchase}";
    public const string CanManageFinance = $"{Admin},{Manager},{Accountant}";
    public const string CanViewReports = $"{Admin},{Manager},{Accountant}";
    public const string CanManageMasterData = $"{Admin},{Manager},{Sales},{Purchase},{Inventory}";
}
