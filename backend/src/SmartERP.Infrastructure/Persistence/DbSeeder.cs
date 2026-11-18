using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartERP.Application.Common;
using SmartERP.Domain.Entities;
using SmartERP.Domain.Enums;

namespace SmartERP.Infrastructure.Persistence;

/// <summary>
/// Seeds roles/users and (optionally) ~14 months of realistic demo trading history so the dashboard,
/// forecasting and AI features have meaningful data. Deterministic (fixed random seed).
/// </summary>
public class DbSeeder(AppDbContext db, IPasswordService passwords, ILogger<DbSeeder> log)
{
    private readonly Random _r = new(42);
    private readonly Dictionary<string, int> _seq = new();

    public async Task SeedAsync(bool demoData, CancellationToken ct = default)
    {
        await SeedIdentityAsync(ct);
        if (demoData && !await db.Products.AnyAsync(ct))
        {
            log.LogInformation("Seeding demo ERP data (this takes a few seconds)…");
            await SeedDemoAsync(ct);
            log.LogInformation("Demo data seeded.");
        }
    }

    private async Task SeedIdentityAsync(CancellationToken ct)
    {
        var descriptions = new Dictionary<string, string>
        {
            [Roles.Admin] = "Full access including user administration",
            [Roles.Manager] = "All business modules and reports",
            [Roles.Sales] = "Customers, quotations, sales orders",
            [Roles.Purchase] = "Suppliers, purchase orders, receiving",
            [Roles.Inventory] = "Products, warehouses, stock movements",
            [Roles.Accountant] = "Invoices, payments, expenses, financial reports",
        };
        foreach (var (name, desc) in descriptions)
            if (!await db.Roles.AnyAsync(r => r.Name == name, ct)) db.Roles.Add(new Role { Name = name, Description = desc });
        await db.SaveChangesAsync(ct);

        if (await db.Users.AnyAsync(ct)) return;
        var roles = await db.Roles.ToDictionaryAsync(r => r.Name, ct);
        (string User, string Name, string Pwd, string[] Roles)[] users =
        [
            ("admin", "System Administrator", "Admin@123", [Roles.Admin]),
            ("manager", "Priya Sharma", "Manager@123", [Roles.Manager]),
            ("sales", "Rahul Verma", "Sales@123", [Roles.Sales]),
            ("purchase", "Anita Desai", "Purchase@123", [Roles.Purchase, Roles.Inventory]),
            ("accounts", "Vikram Iyer", "Accounts@123", [Roles.Accountant]),
        ];
        foreach (var u in users)
        {
            var user = new User { UserName = u.User, FullName = u.Name, Email = $"{u.User}@smarterp.local" };
            user.PasswordHash = passwords.Hash(user, u.Pwd);
            foreach (var r in u.Roles) user.UserRoles.Add(new UserRole { User = user, Role = roles[r] });
            db.Users.Add(user);
        }
        await db.SaveChangesAsync(ct);
    }

    private record P(string Sku, string Name, string Cat, string Unit, decimal Cost, decimal Price, decimal Tax, decimal Reorder, int Supplier, double Demand);

    private async Task SeedDemoAsync(CancellationToken ct)
    {
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        var cats = new[] { "Laptops & Computers", "Monitors & Printers", "Networking", "Accessories", "Office Supplies", "Furniture" }
            .Select(n => new Category { Name = n, Description = $"{n} product line" }).ToDictionary(c => c.Name);
        db.Categories.AddRange(cats.Values);

        var main = new Warehouse { Code = "MAIN", Name = "Main Warehouse", Location = "Andheri East, Mumbai" };
        var pune = new Warehouse { Code = "PUN", Name = "Pune Distribution Centre", Location = "Hinjewadi, Pune" };
        db.Warehouses.AddRange(main, pune);

        var suppliers = new[]
        {
            new Supplier { Code = "SUP0001", Name = "Redington India Ltd", ContactPerson = "Suresh Nair", Email = "orders@redington.example", Phone = "+91 22 4000 1100", City = "Chennai", TaxNumber = "33AABCR1234F1Z5", LeadTimeDays = 5 },
            new Supplier { Code = "SUP0002", Name = "Ingram Micro India", ContactPerson = "Meera Kapoor", Email = "sales@ingram.example", Phone = "+91 22 4000 2200", City = "Mumbai", TaxNumber = "27AABCI5678G1Z2", LeadTimeDays = 7 },
            new Supplier { Code = "SUP0003", Name = "Supertron Electronics", ContactPerson = "Amit Bose", Email = "trade@supertron.example", Phone = "+91 33 4000 3300", City = "Kolkata", TaxNumber = "19AABCS9012H1Z8", LeadTimeDays = 6 },
            new Supplier { Code = "SUP0004", Name = "NetLink Distributors", ContactPerson = "Karan Malhotra", Email = "po@netlink.example", Phone = "+91 80 4000 4400", City = "Bengaluru", TaxNumber = "29AABCN3456J1Z1", LeadTimeDays = 6 },
            new Supplier { Code = "SUP0005", Name = "Navneet Office Supplies", ContactPerson = "Ritu Jain", Email = "b2b@navneet.example", Phone = "+91 79 4000 5500", City = "Ahmedabad", TaxNumber = "24AABCN7890K1Z4", LeadTimeDays = 4 },
            new Supplier { Code = "SUP0006", Name = "Woodcraft Interiors", ContactPerson = "Deepak Patil", Email = "orders@woodcraft.example", Phone = "+91 20 4000 6600", City = "Pune", TaxNumber = "27AABCW2345L1Z7", LeadTimeDays = 14 },
        };
        db.Suppliers.AddRange(suppliers);

        P[] catalog =
        [
            new("LAP-DL5440", "Dell Latitude 5440 Laptop", "Laptops & Computers", "pcs", 62000, 74999, 18, 5, 0, 8),
            new("LAP-HP450", "HP ProBook 450 G10 Laptop", "Laptops & Computers", "pcs", 58000, 69999, 18, 5, 1, 7),
            new("LAP-LNE14", "Lenovo ThinkPad E14 Laptop", "Laptops & Computers", "pcs", 55000, 66500, 18, 4, 0, 6),
            new("PC-DLOPT", "Dell OptiPlex 7010 Desktop", "Laptops & Computers", "pcs", 48000, 57999, 18, 3, 0, 4),
            new("MON-SM24", "Samsung 24\" FHD LED Monitor", "Monitors & Printers", "pcs", 9000, 11999, 18, 10, 2, 18),
            new("MON-LG27", "LG 27\" 4K UHD Monitor", "Monitors & Printers", "pcs", 21000, 26999, 18, 6, 2, 9),
            new("PRN-EP3250", "Epson EcoTank L3250 Printer", "Monitors & Printers", "pcs", 11500, 14499, 18, 6, 1, 10),
            new("PRN-HPLJ", "HP LaserJet Pro M126nw MFP", "Monitors & Printers", "pcs", 17500, 21999, 18, 4, 1, 5),
            new("NET-AX55", "TP-Link Archer AX55 Wi-Fi 6 Router", "Networking", "pcs", 5200, 6999, 18, 10, 3, 20),
            new("NET-CS24", "Cisco CBS250 24-Port Gigabit Switch", "Networking", "pcs", 14500, 18999, 18, 4, 3, 5),
            new("NET-CAT6", "Cat6 Ethernet Cable Box 305m", "Networking", "box", 6200, 8499, 18, 8, 3, 12),
            new("NET-UPS1K", "APC 1kVA Line-Interactive UPS", "Networking", "pcs", 7800, 9999, 18, 6, 3, 9),
            new("ACC-MK270", "Logitech MK270 Wireless Keyboard & Mouse", "Accessories", "pcs", 1350, 1995, 18, 30, 1, 60),
            new("ACC-M331", "Logitech M331 Silent Mouse", "Accessories", "pcs", 850, 1299, 18, 40, 1, 80),
            new("ACC-DOCK", "HP USB-C Dock G5", "Accessories", "pcs", 13000, 16999, 18, 5, 1, 7),
            new("ACC-SSD1T", "SanDisk 1TB Portable SSD", "Accessories", "pcs", 6800, 8999, 18, 12, 2, 25),
            new("ACC-BAG15", "Laptop Backpack 15.6\"", "Accessories", "pcs", 900, 1499, 12, 25, 2, 40),
            new("ACC-HDST", "Jabra Evolve2 30 Headset", "Accessories", "pcs", 4200, 5799, 18, 10, 2, 16),
            new("OFS-A4", "A4 Copier Paper 75 GSM (500 sheets)", "Office Supplies", "ream", 230, 320, 12, 150, 4, 300),
            new("OFS-EP003", "Epson 003 Ink Bottle Set (4 colours)", "Office Supplies", "set", 1450, 1999, 18, 30, 4, 45),
            new("OFS-STPL", "Kangaro Heavy Duty Stapler", "Office Supplies", "pcs", 280, 449, 18, 20, 4, 25),
            new("OFS-WBM", "Whiteboard Markers (Pack of 10)", "Office Supplies", "pack", 210, 349, 18, 40, 4, 60),
            new("FUR-CHAIR", "Ergonomic Mesh Office Chair", "Furniture", "pcs", 6500, 9499, 18, 8, 5, 15),
            new("FUR-DESK5", "Executive Office Desk 5ft", "Furniture", "pcs", 11000, 15999, 18, 4, 5, 6),
            new("FUR-FILE4", "Steel Filing Cabinet 4-Drawer", "Furniture", "pcs", 8200, 11499, 18, 3, 5, 4),
            new("FUR-CONF8", "Conference Table 8-Seater", "Furniture", "pcs", 26000, 34999, 18, 2, 5, 1.5),
        ];

        var products = catalog.Select(c => new Product
        {
            Sku = c.Sku, Name = c.Name, Category = cats[c.Cat], Unit = c.Unit, CostPrice = c.Cost, SalePrice = c.Price,
            TaxRate = c.Tax, ReorderLevel = c.Reorder, PreferredSupplier = suppliers[c.Supplier],
            Description = $"{c.Name} — {c.Cat}"
        }).ToArray();
        db.Products.AddRange(products);

        (string Name, string City, double Weight)[] customerData =
        [
            ("Apex Technologies Pvt Ltd", "Mumbai", 3), ("Bluestone Consulting LLP", "Pune", 2), ("Coral Infosystems", "Bengaluru", 2.5),
            ("Delta Logistics Ltd", "Navi Mumbai", 1.5), ("Evergreen Schools Trust", "Thane", 2), ("Fusion Healthcare Pvt Ltd", "Mumbai", 1.8),
            ("Greenfield Realty", "Pune", 1), ("Horizon Bank Branch Ops", "Mumbai", 2.2), ("Indus Pharma Ltd", "Ahmedabad", 1.6),
            ("Jupiter Retail Chain", "Nashik", 1.4), ("Kaveri Textiles", "Surat", 0.8), ("Lotus Hospitality Group", "Goa", 1.2),
            ("Metro Coworking Spaces", "Mumbai", 2.6), ("Nova Engineering Works", "Nagpur", 0.9), ("Orbit Media House", "Pune", 1.1),
            ("Pinnacle CA Associates", "Mumbai", 0.7), ("Quantum Analytics", "Bengaluru", 1.9), ("Riverdale College", "Kolhapur", 1.3),
        ];
        var customers = customerData.Select((c, i) => new Customer
        {
            Code = $"CUS{i + 1:D4}", Name = c.Name, City = c.City, Email = $"accounts@{Slug(c.Name)}.example",
            Phone = $"+91 9{_r.Next(100000000, 999999999)}", Address = $"{_r.Next(1, 400)}, Business Park, {c.City}",
            CreditLimit = (decimal)(c.Weight * 500000), TaxNumber = $"27AAB{(char)('A' + i)}{_r.Next(1000, 9999)}X1Z{i % 9}"
        }).ToArray();
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync(ct);

        // ---------- simulation ----------
        var now = DateTime.UtcNow;
        var thisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var start = thisMonth.AddMonths(-13);
        var stock = products.ToDictionary(p => p.Id, _ => 0m);
        var weights = catalog.Select(c => Math.Sqrt(c.Demand)).ToArray();
        var custWeights = customerData.Select(c => c.Weight).ToArray();
        // products intentionally left short at the end so low-stock / reorder features have something to show
        var leaveShort = new HashSet<int> { products[0].Id, products[4].Id, products[8].Id, products[12].Id, products[18].Id, products[22].Id };

        // opening balances
        foreach (var (p, c) in products.Zip(catalog))
        {
            var qty = Math.Ceiling((decimal)c.Demand * 2.5m);
            Stock(p, main.Id, StockTransactionType.OpeningBalance, qty, p.CostPrice, "OPENING", start.AddDays(-1), stock);
        }
        await db.SaveChangesAsync(ct);

        for (var month = start; month <= thisMonth; month = month.AddMonths(1))
        {
            var isCurrent = month == thisMonth;
            var monthIndex = (month.Year - start.Year) * 12 + month.Month - start.Month;
            var growth = Math.Pow(1.025, monthIndex);
            var season = month.Month switch { 3 => 1.30, 10 => 1.20, 11 => 1.25, 12 => 1.05, 6 => 0.85, 7 => 0.80, 8 => 0.90, 1 => 0.95, _ => 1.0 };
            var lastDay = isCurrent ? now : month.AddMonths(1).AddSeconds(-1);
            var fraction = isCurrent ? Math.Max(0.15, (now - month).TotalDays / DateTime.DaysInMonth(month.Year, month.Month)) : 1.0;
            var factor = growth * season;

            // replenish at the start of the month
            var toBuy = new Dictionary<int, List<(Product P, decimal Qty)>>();
            foreach (var (p, c) in products.Zip(catalog))
            {
                if (isCurrent && leaveShort.Contains(p.Id)) continue;
                var need = (decimal)(c.Demand * factor * 1.4);
                if (stock[p.Id] < need)
                {
                    var qty = Math.Ceiling(need * 1.6m - stock[p.Id]);
                    if (!toBuy.TryGetValue(c.Supplier, out var list)) toBuy[c.Supplier] = list = [];
                    list.Add((p, qty));
                }
            }
            foreach (var (supIdx, lines) in toBuy)
            {
                // ordered late in the previous month, received in the first two days of this month
                var orderDate = month.AddDays(-Math.Min(suppliers[supIdx].LeadTimeDays, 10)).AddHours(_r.Next(9, 17));
                var received = month.AddDays(_r.Next(0, 2)).AddHours(_r.Next(9, 17));
                if (received > now) received = now.AddMinutes(-30);
                var po = NewPurchaseOrder(suppliers[supIdx], main, orderDate, lines);
                po.Status = PurchaseOrderStatus.Received;
                po.ReceivedDate = received;
                po.SupplierInvoiceNumber = $"{suppliers[supIdx].Code[3..]}/{received:yy}/{_r.Next(1000, 9999)}";
                db.PurchaseOrders.Add(po);
                foreach (var (p, qty) in lines) Stock(p, main.Id, StockTransactionType.PurchaseReceipt, qty, p.CostPrice, po.OrderNumber, received, stock);

                var age = (now - received).TotalDays;
                if (age > 30 || _r.NextDouble() < 0.4)
                {
                    var payDate = received.AddDays(_r.Next(5, (int)Math.Clamp(age, 6, 30)));
                    if (payDate < now) AddPayment(PaymentDirection.Outgoing, po.TotalAmount, payDate, null, po);
                }
            }

            // sales
            var orderCount = (int)Math.Round(55 * factor * fraction * (0.9 + _r.NextDouble() * 0.2));
            var avgLines = 2.2;
            var totalWeight = weights.Sum();
            var firstSale = month.AddDays(3); // after the month's replenishment has been received
            var orderDates = Enumerable.Range(0, orderCount)
                .Select(_ => firstSale.AddSeconds(_r.NextDouble() * Math.Max(3600, (lastDay - firstSale).TotalSeconds)))
                .Select(d => d > now ? now.AddMinutes(-5) : d).OrderBy(d => d).ToList();
            foreach (var date in orderDates)
            {
                var customer = customers[Pick(custWeights)];
                var lineCount = 1 + Pick([0.35, 0.35, 0.2, 0.1]);
                var chosen = new HashSet<int>();
                var lines = new List<(Product P, decimal Qty)>();
                for (var l = 0; l < lineCount; l++)
                {
                    var idx = Pick(weights);
                    if (!chosen.Add(idx)) continue;
                    var p = products[idx];
                    var expectedPicks = 55 * factor * avgLines * weights[idx] / totalWeight;
                    var meanQty = catalog[idx].Demand * factor / Math.Max(expectedPicks, 0.5);
                    var qty = Math.Max(1, Math.Round((decimal)(meanQty * (0.5 + _r.NextDouble()))));
                    qty = Math.Min(qty, stock[p.Id]);
                    if (qty > 0) lines.Add((p, qty));
                }
                if (lines.Count == 0) continue;

                var recent = (now - date).TotalDays < 6;
                var status = recent && _r.NextDouble() < 0.35 ? SalesOrderStatus.Quotation
                    : _r.NextDouble() < 0.02 ? SalesOrderStatus.Cancelled : SalesOrderStatus.Invoiced;
                var so = NewSalesOrder(customer, main, date, lines, status);
                db.SalesOrders.Add(so);

                if (status != SalesOrderStatus.Invoiced) continue;
                foreach (var (p, qty) in lines) Stock(p, main.Id, StockTransactionType.SaleIssue, -qty, p.CostPrice, so.OrderNumber, date, stock);

                var inv = new Invoice
                {
                    InvoiceNumber = Next("INV", date), SalesOrder = so, Customer = customer, InvoiceDate = date, DueDate = date.AddDays(30),
                    TotalAmount = so.TotalAmount, CreatedAt = date
                };
                db.Invoices.Add(inv);

                var age = (now - date).TotalDays;
                var (pFull, pPartial) = age > 75 ? (0.97, 0.02) : age > 40 ? (0.75, 0.1) : age > 20 ? (0.45, 0.15) : (0.2, 0.1);
                var roll = _r.NextDouble();
                if (roll < pFull + pPartial)
                {
                    var amount = roll < pFull ? inv.TotalAmount : Math.Round(inv.TotalAmount * (decimal)(0.3 + _r.NextDouble() * 0.4), 0);
                    var payDate = date.AddDays(_r.Next(2, (int)Math.Clamp(age, 3, 45)));
                    if (payDate > now) payDate = now.AddMinutes(-1);
                    AddPayment(PaymentDirection.Incoming, amount, payDate, inv, null);
                    inv.AmountPaid = amount;
                    inv.Status = amount >= inv.TotalAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
                }
            }

            // expenses
            var expDay = (int day) => { var d = month.AddDays(day - 1).AddHours(11); return d > now ? (DateTime?)null : d; };
            void Expense(int day, ExpenseCategory cat, string desc, decimal amount, PaymentMethod method)
            {
                if (expDay(day) is not { } d) return;
                db.Expenses.Add(new Expense { ExpenseDate = d, Category = cat, Description = desc, Amount = Math.Round(amount, 0), Method = method, CreatedAt = d });
            }
            var label = month.ToString("MMM yyyy");
            Expense(1, ExpenseCategory.Rent, $"Office & warehouse rent — {label}", 85000, PaymentMethod.BankTransfer);
            Expense(28, ExpenseCategory.Salaries, $"Staff salaries — {label}", (decimal)(420000 * Math.Pow(1.01, monthIndex)), PaymentMethod.BankTransfer);
            Expense(10, ExpenseCategory.Utilities, $"Electricity & internet — {label}", 16000 + _r.Next(0, 6000), PaymentMethod.BankTransfer);
            Expense(15, ExpenseCategory.Marketing, $"Digital marketing campaign — {label}", 25000 + _r.Next(0, 35000), PaymentMethod.Card);
            Expense(20, ExpenseCategory.Transport, $"Delivery & freight charges — {label}", 12000 + _r.Next(0, 14000), PaymentMethod.Upi);
            Expense(5, ExpenseCategory.OfficeSupplies, $"Office consumables — {label}", 4000 + _r.Next(0, 6000), PaymentMethod.Cash);
            if (_r.NextDouble() < 0.4) Expense(18, ExpenseCategory.Maintenance, "AC & equipment servicing", 6000 + _r.Next(0, 12000), PaymentMethod.Cheque);

            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync(ct);
        }

        // open purchase orders (one ordered, one draft) for the short items' suppliers
        var shortProducts = products.Where(p => leaveShort.Contains(p.Id)).Take(2).ToList();
        if (shortProducts.Count == 2)
        {
            var ordered = NewPurchaseOrder(shortProducts[0].PreferredSupplier!, main, now.AddDays(-2), [(shortProducts[0], 10m)]);
            ordered.Status = PurchaseOrderStatus.Ordered;
            ordered.ExpectedDate = now.AddDays(4);
            var draft = NewPurchaseOrder(shortProducts[1].PreferredSupplier!, main, now.AddHours(-3), [(shortProducts[1], 30m)]);
            db.PurchaseOrders.AddRange(ordered, draft);
        }

        // a recent stock count found shrinkage on a few items, pushing them below their reorder levels
        var targets = new[] { 0.0, 0.3, 0.6, 0.5, 0.8, 0.4 };
        var k = 0;
        foreach (var p in products.Where(p => leaveShort.Contains(p.Id)))
        {
            var target = Math.Floor(p.ReorderLevel * (decimal)targets[k++ % targets.Length]);
            var diff = stock[p.Id] - target;
            if (diff > 0)
                Stock(p, main.Id, StockTransactionType.AdjustmentOut, -diff, p.CostPrice, "ADJ-COUNT", now.AddDays(-1), stock);
        }
        foreach (var t in db.ChangeTracker.Entries<StockTransaction>().Where(e => e.State == EntityState.Added && e.Entity.Reference == "ADJ-COUNT"))
            t.Entity.Notes = "Physical stock count variance";

        // sync on-hand quantities from the simulated ledger
        foreach (var p in products) p.QuantityOnHand = stock[p.Id];
        db.ChangeTracker.DetectChanges();
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.AutoDetectChangesEnabled = true;
    }

    // ---------- helpers ----------

    private SalesOrder NewSalesOrder(Customer c, Warehouse w, DateTime date, List<(Product P, decimal Qty)> lines, SalesOrderStatus status)
    {
        var so = new SalesOrder { OrderNumber = Next("SO", date), Customer = c, WarehouseId = w.Id, OrderDate = date, Status = status, CreatedAt = date };
        foreach (var (p, q) in lines)
        {
            var price = p.SalePrice * (decimal)(_r.NextDouble() < 0.2 ? 0.95 : 1.0);
            var net = Math.Round(q * price, 2);
            so.Details.Add(new SalesOrderDetail { Product = p, Quantity = q, UnitPrice = price, TaxRate = p.TaxRate, LineTotal = net + Math.Round(net * p.TaxRate / 100, 2) });
        }
        so.SubTotal = so.Details.Sum(d => Math.Round(d.Quantity * d.UnitPrice, 2));
        so.TaxAmount = so.Details.Sum(d => d.LineTotal) - so.SubTotal;
        so.Discount = so.SubTotal > 200000 && _r.NextDouble() < 0.3 ? Math.Round(so.SubTotal * 0.02m, 0) : 0;
        so.TotalAmount = so.SubTotal + so.TaxAmount - so.Discount;
        return so;
    }

    private PurchaseOrder NewPurchaseOrder(Supplier s, Warehouse w, DateTime date, List<(Product P, decimal Qty)> lines)
    {
        var po = new PurchaseOrder { OrderNumber = Next("PO", date), Supplier = s, WarehouseId = w.Id, OrderDate = date, CreatedAt = date, Status = PurchaseOrderStatus.Draft };
        foreach (var (p, q) in lines)
        {
            var net = Math.Round(q * p.CostPrice, 2);
            po.Details.Add(new PurchaseOrderDetail { Product = p, Quantity = q, UnitCost = p.CostPrice, TaxRate = p.TaxRate, LineTotal = net + Math.Round(net * p.TaxRate / 100, 2) });
        }
        po.SubTotal = po.Details.Sum(d => Math.Round(d.Quantity * d.UnitCost, 2));
        po.TaxAmount = po.Details.Sum(d => d.LineTotal) - po.SubTotal;
        po.TotalAmount = po.SubTotal + po.TaxAmount;
        return po;
    }

    private void Stock(Product p, int warehouseId, StockTransactionType type, decimal qty, decimal cost, string reference, DateTime date, Dictionary<int, decimal> ledger)
    {
        ledger[p.Id] += qty;
        db.StockTransactions.Add(new StockTransaction
        {
            Product = p, WarehouseId = warehouseId, Type = type, Quantity = qty, UnitCost = cost, Reference = reference,
            TransactionDate = date, CreatedAt = date
        });
    }

    private void AddPayment(PaymentDirection dir, decimal amount, DateTime date, Invoice? inv, PurchaseOrder? po)
    {
        var methods = new[] { PaymentMethod.BankTransfer, PaymentMethod.BankTransfer, PaymentMethod.Upi, PaymentMethod.Cheque, PaymentMethod.Card };
        db.Payments.Add(new Payment
        {
            PaymentNumber = Next(dir == PaymentDirection.Incoming ? "RCV" : "PAY", date), Direction = dir, Amount = amount, PaymentDate = date,
            Method = methods[_r.Next(methods.Length)], Invoice = inv, PurchaseOrder = po, CreatedAt = date,
            Reference = $"UTR{_r.Next(10000000, 99999999)}"
        });
        if (po != null) po.AmountPaid += amount;
    }

    private string Next(string prefix, DateTime date)
    {
        var stem = $"{prefix}-{date:yyyyMM}-";
        _seq[stem] = _seq.GetValueOrDefault(stem) + 1;
        return $"{stem}{_seq[stem]:D4}";
    }

    private int Pick(double[] weights)
    {
        var x = _r.NextDouble() * weights.Sum();
        for (var i = 0; i < weights.Length; i++) { x -= weights[i]; if (x <= 0) return i; }
        return weights.Length - 1;
    }

    private static string Slug(string s) => new(s.ToLowerInvariant().Where(char.IsLetterOrDigit).Take(14).ToArray());
}
