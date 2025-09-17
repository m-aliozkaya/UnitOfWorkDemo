using Microsoft.AspNetCore.Mvc;
using UnitOfWorkDemo.Data.UnitOfWork;
using UnitOfWorkDemo.Models;

namespace UnitOfWorkDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public ProductsController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            var repository = _unitOfWork.Repository<Product>();
            var products = await repository.GetAllAsync();
            return Ok(products);
        }

        [HttpPost]
        public async Task<IActionResult> CreateProduct(Product product)
        {
            var repository = _unitOfWork.Repository<Product>();
            await repository.AddAsync(product);
            await _unitOfWork.SaveChangesAsync();
            return Ok(product);
        }

    }
}