# SmartERP – AI-Powered Business Management System

A full-stack ERP for small and medium businesses. It covers inventory, sales, purchasing, invoicing, payments, expenses and reports, and adds an **AI layer** that answers questions about your live ERP data, forecasts demand, recommends reorders, writes business reports and reads supplier invoices.

**Stack:** React 19 + TypeScript + Vite · Material UI · Redux Toolkit (RTK Query) · Recharts · ASP.NET Core 10 Web API · Clean Architecture · Entity Framework Core 10 · SQL Server · JWT + refresh tokens · OpenAI API · Swagger

---

## Quick start

### Prerequisites
- .NET SDK 10
- Node.js 20+
- SQL Server. The default connection string targets `.\SQLEXPRESS` with Windows authentication. Change it in `backend/src/SmartERP.Api/appsettings.json` if you use a different server, LocalDB (`(localdb)\MSSQLLocalDB`) or SQL authentication.

### 1. Run the API
```bash
npm run api
# or: cd backend/src/SmartERP.Api && dotnet run --launch-profile http
```
On first start the API **creates the `SmartERP` database** by applying the EF Core migrations, then **seeds about 14 months of realistic demo data**: customers, suppliers, 26 products, sales, purchases, payments and expenses.
- API: http://localhost:5080
- Swagger UI: http://localhost:5080/swagger
- Health check: http://localhost:5080/health

### 2. Run the React app
```bash
npm install
npm run dev
```
Open http://localhost:5173. Vite proxies `/api` to the .NET API.

### Demo accounts
| User | Password | Role |
|---|---|---|
| admin | Admin@123 | Admin (everything, including user management) |
| manager | Manager@123 | Manager |
| sales | Sales@123 | Sales |
| purchase | Purchase@123 | Purchase + Inventory |
| accounts | Accounts@123 | Accountant |

### 3. (Optional) Enable OpenAI
Without a key, every AI feature still works in **offline mode** (built-in rules and templates over the same data), except the invoice reader, which needs a vision model.

```bash
cd backend/src/SmartERP.Api
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
dotnet user-secrets set "OpenAI:Model" "gpt-4.1-mini"   # optional; any tool-calling + vision model
```
You can also use the environment variable `OpenAI__ApiKey`. `OpenAI:BaseUrl` accepts any OpenAI-compatible endpoint.

---

## Features

| Module | What it does |
|---|---|
| Dashboard | KPIs (month-to-date vs the same period last month), 12-month sales/profit/purchases trend, sales by category, top products, receivables aging, low-stock alerts, recent orders |
| Inventory | Products, categories, warehouses, auditable stock ledger, manual adjustments, low-stock filter |
| Sales | Quotations → **Confirm** (issues stock and raises the invoice in one DB transaction) → cancel/reverse |
| Purchasing | Draft → Ordered → **Receive** (stock in with weighted-average costing, one DB transaction) |
| Finance | Invoices (printable), customer receipts, supplier payments, expenses, overdue tracking |
| Customers / Suppliers | Profiles, credit limits, lead times, balances, full transaction history |
| Reports | Sales and purchases grouped by day/month/customer/supplier/product/category, P&L, inventory valuation, receivables aging, CSV export |
| Administration | Users, roles, activation, password reset |

### AI features
1. **AI ERP Assistant** (`/ai/assistant`): ask natural-language questions such as *"What were this month's total sales?"*, *"Which products are low in stock?"* or *"Show pending customer payments"*.
2. **AI Sales Forecasting** (`/ai/insights`): Holt's linear-trend exponential smoothing with grid-searched parameters, an 80% prediction band and back-test MAPE, for total sales or any single product. AI commentary explains it.
3. **Smart Inventory Management**: reorder points from 90-day demand, demand variability (95% service-level safety stock), supplier lead time and quantities already on order, with urgency levels. A one-click **Create PO** button groups items by supplier.
4. **AI Report Generator**: monthly sales summary, purchase report, inventory health or business overview, written as markdown by the LLM from figures computed in .NET.
5. **AI Invoice Reader** (`/ai/invoice-reader`): upload a PDF or image. A vision model returns schema-validated JSON (strict `json_schema`); SmartERP fuzzy-matches the supplier and products, cross-checks the totals and pre-fills a purchase order.

### How the AI stays safe
The LLM **never gets database access or writes SQL**. The assistant uses OpenAI tool calling over a **whitelist of read-only .NET service methods** (`AssistantTools.cs`), such as `get_sales_summary`, `get_low_stock_products` and `get_pending_customer_payments`. The model can only choose a tool and supply validated parameters. .NET runs the query and returns aggregated JSON. Other protections:
- Prompts tell the model to treat tool output and uploaded documents as data, not instructions.
- Tool results are size-capped and tool rounds are limited.
- AI endpoints are rate-limited per user (20/min).
- Numbers in reports and forecasts are computed by .NET, and the LLM only writes the narrative.

---

## Architecture

```
React + TypeScript (Vite, MUI, RTK Query, Recharts)
        │  /api  (JWT bearer, auto refresh-token rotation)
        ▼
SmartERP.Api             Controllers · auth · role policies · rate limiting · ProblemDetails · Swagger
SmartERP.Application     Business services · DTOs · validation · analytics · forecasting · AI orchestration
SmartERP.Infrastructure  EF Core DbContext + migrations · JWT/PBKDF2 · OpenAI client · demo seeder
SmartERP.Domain          Entities & enums (no dependencies)
        │                                   │
        ▼                                   ▼
   SQL Server                          OpenAI API
```

### Database
Tables: Users, Roles, UserRoles, RefreshTokens, Customers, Suppliers, Categories, Products, Warehouses, StockTransactions, SalesOrders, SalesOrderDetails, PurchaseOrders, PurchaseOrderDetails, Invoices, Payments, Expenses.
- Primary and foreign keys with explicit delete behaviour (restrict for business data, cascade for document lines).
- Unique indexes on document numbers, SKUs, codes and user names. Composite indexes for reporting queries (`Status + Date`, `ProductId + TransactionDate`, …).
- **Check constraints**: stock can't go negative, payment amount must be positive, `AmountPaid ≤ TotalAmount`, and a payment targets exactly one invoice or one PO.
- `rowversion` optimistic concurrency on products. Multi-step operations run in explicit DB transactions.

### Security
- JWT access tokens (15 min) plus refresh tokens (7 days). Refresh tokens are **stored hashed (SHA-256), rotated on every use**, and **reuse detection** revokes every session of that user.
- PBKDF2 password hashing (ASP.NET Core Identity hasher).
- Role-based authorization on every controller and action. The UI hides what a role can't do, and the API enforces it.
- Before deploying, change `Jwt:Key` (via user-secrets or an environment variable) and the demo passwords.

### Useful commands
```bash
# new migration after changing entities
cd backend
dotnet ef migrations add <Name> --project src/SmartERP.Infrastructure --startup-project src/SmartERP.Api --output-dir Persistence/Migrations

# reset the demo database (re-seeded on the next API start)
dotnet ef database drop -f --project src/SmartERP.Infrastructure --startup-project src/SmartERP.Api

npm run lint && npm run build   # frontend checks
```
Set `Seed:DemoData` to `false` to start with an empty database. Roles and the admin user are still created.

---

## Resume summary

**SmartERP – AI-Powered Business Management System**
*React.js, TypeScript, ASP.NET Core Web API, Entity Framework Core, SQL Server, OpenAI API*
- Developed a full-stack ERP application for managing inventory, sales, purchases, customers, suppliers and financial transactions.
- Designed RESTful APIs with ASP.NET Core using Clean Architecture, with EF Core persistence, transactional stock and invoicing workflows, and SQL Server constraints and indexes.
- Implemented JWT authentication with rotating refresh tokens, reuse detection and role-based access control.
- Integrated the OpenAI API for a tool-calling ERP assistant over secure read-only services, AI-generated reports, invoice data extraction with vision models, and intelligent reorder recommendations.
- Built statistical sales forecasting (Holt's method with prediction intervals) and interactive dashboards and reports with Recharts and Material UI.
