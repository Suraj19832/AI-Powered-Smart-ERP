// Mirrors SmartERP.Application.Common.Roles role groups.
export const R = {
  admin: ["Admin"],
  sell: ["Admin", "Manager", "Sales"],
  buy: ["Admin", "Manager", "Purchase"],
  stock: ["Admin", "Manager", "Inventory", "Purchase"],
  finance: ["Admin", "Manager", "Accountant"],
  reports: ["Admin", "Manager", "Accountant"],
  managers: ["Admin", "Manager"],
};
