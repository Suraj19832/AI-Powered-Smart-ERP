using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartERP.Api.Infrastructure;
using SmartERP.Application.Common;
using SmartERP.Application.Inventory;
using SmartERP.Application.MasterData;

namespace SmartERP.Api.Controllers;

[ApiController, Route("api/customers"), Authorize]
public class CustomersController(CustomerService svc) : ControllerBase
{
    [HttpGet] public Task<PagedResult<CustomerDto>> List([FromQuery] PagedQuery q, CancellationToken ct) => svc.ListAsync(q, ct);
    [HttpGet("lookup")] public Task<List<LookupDto>> Lookup(CancellationToken ct) => svc.LookupAsync(ct);
    [HttpGet("{id:int}")] public Task<CustomerDetailDto> Get(int id, CancellationToken ct) => svc.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = Roles.CanSell)]
    public async Task<IActionResult> Create(CustomerInput input, CancellationToken ct) => Ok(new { id = await svc.CreateAsync(input, ct) });

    [HttpPut("{id:int}"), Authorize(Roles = Roles.CanSell)]
    public async Task<IActionResult> Update(int id, CustomerInput input, CancellationToken ct) { await svc.UpdateAsync(id, input, ct); return NoContent(); }

    [HttpDelete("{id:int}"), Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController, Route("api/suppliers"), Authorize]
public class SuppliersController(SupplierService svc) : ControllerBase
{
    [HttpGet] public Task<PagedResult<SupplierDto>> List([FromQuery] PagedQuery q, CancellationToken ct) => svc.ListAsync(q, ct);
    [HttpGet("lookup")] public Task<List<LookupDto>> Lookup(CancellationToken ct) => svc.LookupAsync(ct);
    [HttpGet("{id:int}")] public Task<SupplierDetailDto> Get(int id, CancellationToken ct) => svc.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = Roles.CanBuy)]
    public async Task<IActionResult> Create(SupplierInput input, CancellationToken ct) => Ok(new { id = await svc.CreateAsync(input, ct) });

    [HttpPut("{id:int}"), Authorize(Roles = Roles.CanBuy)]
    public async Task<IActionResult> Update(int id, SupplierInput input, CancellationToken ct) { await svc.UpdateAsync(id, input, ct); return NoContent(); }

    [HttpDelete("{id:int}"), Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController, Route("api/categories"), Authorize]
public class CategoriesController(CategoryService svc) : ControllerBase
{
    [HttpGet] public Task<List<CategoryDto>> List(CancellationToken ct) => svc.ListAsync(ct);

    [HttpPost, Authorize(Roles = Roles.CanManageStock)]
    public async Task<IActionResult> Create(CategoryInput input, CancellationToken ct) => Ok(new { id = await svc.CreateAsync(input, ct) });

    [HttpPut("{id:int}"), Authorize(Roles = Roles.CanManageStock)]
    public async Task<IActionResult> Update(int id, CategoryInput input, CancellationToken ct) { await svc.UpdateAsync(id, input, ct); return NoContent(); }

    [HttpDelete("{id:int}"), Authorize(Roles = Roles.CanManageStock)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController, Route("api/warehouses"), Authorize]
public class WarehousesController(WarehouseService svc) : ControllerBase
{
    [HttpGet] public Task<List<WarehouseDto>> List(CancellationToken ct) => svc.ListAsync(ct);

    [HttpPost, Authorize(Roles = Roles.CanManageStock)]
    public async Task<IActionResult> Create(WarehouseInput input, CancellationToken ct) => Ok(new { id = await svc.CreateAsync(input, ct) });

    [HttpPut("{id:int}"), Authorize(Roles = Roles.CanManageStock)]
    public async Task<IActionResult> Update(int id, WarehouseInput input, CancellationToken ct) { await svc.UpdateAsync(id, input, ct); return NoContent(); }

    [HttpDelete("{id:int}"), Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController, Route("api/products"), Authorize]
public class ProductsController(ProductService svc) : ControllerBase
{
    [HttpGet] public Task<PagedResult<ProductDto>> List([FromQuery] ProductQuery q, CancellationToken ct) => svc.ListAsync(q, ct);
    [HttpGet("lookup")] public Task<List<LookupDto>> Lookup(CancellationToken ct) => svc.LookupAsync(ct);
    [HttpGet("{id:int}")] public Task<ProductDto> Get(int id, CancellationToken ct) => svc.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = Roles.CanManageStock)]
    public async Task<IActionResult> Create(ProductInput input, CancellationToken ct) => Ok(new { id = await svc.CreateAsync(input, ct) });

    [HttpPut("{id:int}"), Authorize(Roles = Roles.CanManageStock)]
    public async Task<IActionResult> Update(int id, ProductInput input, CancellationToken ct) { await svc.UpdateAsync(id, input, ct); return NoContent(); }

    [HttpDelete("{id:int}"), Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController, Route("api/stock"), Authorize]
public class StockController(StockService svc) : ControllerBase
{
    [HttpGet("transactions")] public Task<PagedResult<StockTransactionDto>> List([FromQuery] StockQuery q, CancellationToken ct) => svc.ListAsync(q, ct);

    [HttpPost("adjust"), Authorize(Roles = Roles.CanManageStock)]
    public async Task<IActionResult> Adjust(StockAdjustmentRequest req, CancellationToken ct) { await svc.AdjustAsync(req, User.UserName(), ct); return NoContent(); }
}
