using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartERP.Application.Common;
using SmartERP.Application.Finance;
using SmartERP.Application.Purchasing;
using SmartERP.Application.Sales;

namespace SmartERP.Api.Controllers;

[ApiController, Route("api/sales-orders"), Authorize]
public class SalesOrdersController(SalesOrderService svc) : ControllerBase
{
    [HttpGet] public Task<PagedResult<SalesOrderListDto>> List([FromQuery] SalesOrderQuery q, CancellationToken ct) => svc.ListAsync(q, ct);
    [HttpGet("{id:int}")] public Task<SalesOrderDto> Get(int id, CancellationToken ct) => svc.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = Roles.CanSell)]
    public async Task<IActionResult> Create(SalesOrderInput input, CancellationToken ct) => Ok(new { id = await svc.CreateAsync(input, ct) });

    [HttpPut("{id:int}"), Authorize(Roles = Roles.CanSell)]
    public async Task<IActionResult> Update(int id, SalesOrderInput input, CancellationToken ct) { await svc.UpdateAsync(id, input, ct); return NoContent(); }

    /// <summary>Confirms the quotation: issues stock and raises an invoice in one transaction.</summary>
    [HttpPost("{id:int}/confirm"), Authorize(Roles = Roles.CanSell)]
    public async Task<IActionResult> Confirm(int id, CancellationToken ct) => Ok(new { invoiceId = await svc.ConfirmAsync(id, ct) });

    [HttpPost("{id:int}/cancel"), Authorize(Roles = Roles.CanSell)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) { await svc.CancelAsync(id, ct); return NoContent(); }

    [HttpDelete("{id:int}"), Authorize(Roles = Roles.CanSell)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController, Route("api/invoices"), Authorize]
public class InvoicesController(InvoiceService svc) : ControllerBase
{
    [HttpGet] public Task<PagedResult<InvoiceListDto>> List([FromQuery] InvoiceQuery q, CancellationToken ct) => svc.ListAsync(q, ct);
    [HttpGet("{id:int}")] public Task<InvoiceDto> Get(int id, CancellationToken ct) => svc.GetAsync(id, ct);
}

[ApiController, Route("api/purchase-orders"), Authorize]
public class PurchaseOrdersController(PurchaseOrderService svc) : ControllerBase
{
    [HttpGet] public Task<PagedResult<PurchaseOrderListDto>> List([FromQuery] PurchaseOrderQuery q, CancellationToken ct) => svc.ListAsync(q, ct);
    [HttpGet("{id:int}")] public Task<PurchaseOrderDto> Get(int id, CancellationToken ct) => svc.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = Roles.CanBuy)]
    public async Task<IActionResult> Create(PurchaseOrderInput input, CancellationToken ct) => Ok(new { id = await svc.CreateAsync(input, ct) });

    [HttpPut("{id:int}"), Authorize(Roles = Roles.CanBuy)]
    public async Task<IActionResult> Update(int id, PurchaseOrderInput input, CancellationToken ct) { await svc.UpdateAsync(id, input, ct); return NoContent(); }

    [HttpPost("{id:int}/order"), Authorize(Roles = Roles.CanBuy)]
    public async Task<IActionResult> MarkOrdered(int id, CancellationToken ct) { await svc.MarkOrderedAsync(id, ct); return NoContent(); }

    /// <summary>Receives goods into stock (weighted-average cost) in one transaction.</summary>
    [HttpPost("{id:int}/receive"), Authorize(Roles = Roles.CanManageStock)]
    public async Task<IActionResult> Receive(int id, CancellationToken ct) { await svc.ReceiveAsync(id, ct); return NoContent(); }

    [HttpPost("{id:int}/cancel"), Authorize(Roles = Roles.CanBuy)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) { await svc.CancelAsync(id, ct); return NoContent(); }

    [HttpDelete("{id:int}"), Authorize(Roles = Roles.CanBuy)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController, Route("api/payments"), Authorize(Roles = Roles.CanManageFinance)]
public class PaymentsController(PaymentService svc) : ControllerBase
{
    [HttpGet] public Task<PagedResult<PaymentDto>> List([FromQuery] PaymentQuery q, CancellationToken ct) => svc.ListAsync(q, ct);

    [HttpPost]
    public async Task<IActionResult> Record(PaymentInput input, CancellationToken ct) => Ok(new { id = await svc.RecordAsync(input, ct) });
}

[ApiController, Route("api/expenses"), Authorize(Roles = Roles.CanManageFinance)]
public class ExpensesController(ExpenseService svc) : ControllerBase
{
    [HttpGet] public Task<PagedResult<ExpenseDto>> List([FromQuery] ExpenseQuery q, CancellationToken ct) => svc.ListAsync(q, ct);

    [HttpPost]
    public async Task<IActionResult> Create(ExpenseInput input, CancellationToken ct) => Ok(new { id = await svc.CreateAsync(input, ct) });

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ExpenseInput input, CancellationToken ct) { await svc.UpdateAsync(id, input, ct); return NoContent(); }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteAsync(id, ct); return NoContent(); }
}
