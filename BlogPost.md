# Implementing the Unit of Work Pattern in ASP.NET Core: A Practical Guide

The Unit of Work pattern is a design pattern that maintains a list of objects affected by a business transaction and coordinates writing out changes and resolving concurrency problems. This pattern becomes particularly valuable when working with complex business operations that require coordination across multiple entities and repositories.

## The Problem: Transaction Management Complexity

When using only the Repository pattern, applications can face significant challenges with transaction management in complex business scenarios. Consider a typical order creation process that requires multiple coordinated operations:

1. Creating an order record
2. Updating product stock quantities
3. Processing payment information
4. Updating customer loyalty points

Without proper transaction coordination, these operations might be executed separately:

```csharp
// Problematic approach - lacks transaction coordination
await _orderRepository.CreateAsync(order);
await _productRepository.UpdateStockAsync(productId, newStock);
await _paymentRepository.CreateAsync(payment);
await _customerRepository.UpdatePointsAsync(customerId, points);
```

This approach can lead to data inconsistency issues. For example, if the stock update fails after an order is created, the system ends up in an inconsistent state where orders exist for products that may no longer be available. Such scenarios highlight the need for atomic operations that ensure all related changes either succeed together or fail together.

## Understanding the Unit of Work Pattern

The Unit of Work pattern addresses transaction management by coordinating multiple repository operations within a single business transaction. It maintains a list of objects affected by the transaction and ensures that all changes are committed together or rolled back if any operation fails.

Key characteristics of the Unit of Work pattern include:
- **Transaction Coordination**: Manages database transactions across multiple repositories
- **Change Tracking**: Keeps track of all modified objects during a business operation
- **Atomic Operations**: Ensures all operations complete successfully or none at all
- **Resource Management**: Efficiently manages database connections and contexts

The pattern integrates well with Entity Framework Core and ASP.NET Core's dependency injection system, providing a clean separation of concerns between business logic and data persistence.

## Implementation Steps

### Step 1: Project Setup

Create a new ASP.NET Core Web API project and add the necessary Entity Framework packages:

```bash
dotnet new webapi -n UnitOfWorkDemo
cd UnitOfWorkDemo
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools
```

### Step 2: Entity Model Definition

Define a simple `Product` entity to demonstrate the pattern. This simplified example illustrates the core concepts:

```csharp
public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int Stock { get; set; }
}
```

The validation attributes ensure data integrity at the model level and provide client-side validation feedback.

### Step 3: Database Context Configuration

The `ApplicationDbContext` serves as the bridge between our domain entities and the database:

```csharp
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Price).IsRequired().HasPrecision(18, 4);
        });

        // Sample data for demonstration
        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Laptop", Description = "High-performance laptop", Price = 1299.99m, Stock = 15 },
            new Product { Id = 2, Name = "Mouse", Description = "Wireless gaming mouse", Price = 79.99m, Stock = 50 },
            new Product { Id = 3, Name = "Keyboard", Description = "Mechanical keyboard", Price = 149.99m, Stock = 25 }
        );
    }
}
```

The connection string configuration in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=UnitOfWorkDemoDB;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true"
  }
}
```

Note: LocalDB is used here for demonstration purposes. In production environments, you would typically use a full SQL Server instance with appropriate security configurations.

### Step 4: Repository Pattern Implementation

The repository pattern provides an abstraction layer over data access. The generic repository interface defines common operations:

```csharp
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate);

    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);

    Task<bool> ExistsAsync(int id);
    Task<int> CountAsync();
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);
}
```

The base repository implementation provides the common functionality:

```csharp
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    // Additional methods implementation...
}
```

For this tutorial, we'll use only the generic repository to keep things simple and focused on the Unit of Work pattern itself.

### Step 5: Core Unit of Work Implementation

The Unit of Work pattern becomes essential for coordinating multiple repositories. The generic interface defines the contract:

```csharp
public interface IUnitOfWork : IDisposable
{
    IRepository<T> Repository<T>() where T : class;
    Task<int> SaveChangesAsync();
}
```

The implementation coordinates multiple repositories dynamically and manages transactions automatically:

```csharp
public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private bool _disposed = false;

    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public IRepository<T> Repository<T>() where T : class
    {
        var type = typeof(T);

        if (_repositories.ContainsKey(type))
        {
            return (IRepository<T>)_repositories[type];
        }

        IRepository<T> repository = new Repository<T>(_context);

        _repositories.Add(type, repository);
        return repository;
    }

    public async Task<int> SaveChangesAsync()
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var result = await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // Disposal implementation...
}
```

The implementation uses a repository factory pattern with caching. Repositories are created dynamically based on the entity type and cached for subsequent use. The `SaveChangesAsync` method automatically handles transaction management - it begins a transaction, commits on success, or rolls back on failure, ensuring data consistency without requiring manual transaction handling.

### Step 6: Controller Integration

The controller demonstrates practical usage of the Unit of Work pattern with a simple, realistic scenario:

```csharp
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

    [HttpPost("create-bundle")]
    public async Task<IActionResult> CreateGamingBundle()
    {
        var repository = _unitOfWork.Repository<Product>();

        var laptop = new Product
        {
            Name = "Gaming Laptop",
            Description = "High-performance gaming laptop",
            Price = 1500,
            Stock = 10
        };

        var mouse = new Product
        {
            Name = "Gaming Mouse",
            Description = "RGB gaming mouse",
            Price = 50,
            Stock = 10
        };

        var keyboard = new Product
        {
            Name = "Gaming Keyboard",
            Description = "Mechanical gaming keyboard",
            Price = 100,
            Stock = 10
        };

        await repository.AddAsync(laptop);
        await repository.AddAsync(mouse);
        await repository.AddAsync(keyboard);

        await _unitOfWork.SaveChangesAsync();

        return Ok("Gaming bundle created successfully! All 3 products saved in one transaction.");
    }
}
```

### Step 7: Dependency Injection Configuration

Configure the dependency injection container in `Program.cs`:

```csharp
// Entity Framework and SQL Server configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Unit of Work (repositories are created dynamically)
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
```

The `AddScoped` lifetime ensures the same DbContext instance is used throughout a single HTTP request, which is essential for maintaining data consistency and proper transaction handling.

## Correct Usage Pattern

The key insight is that transactions are managed automatically within the `SaveChangesAsync` method. This eliminates the need for manual transaction handling in business logic:

```csharp
// ✅ CORRECT: Simple and clean
public async Task<IActionResult> CreateGamingBundle()
{
    var repository = _unitOfWork.Repository<Product>();

    await repository.AddAsync(laptop);
    await repository.AddAsync(mouse);
    await repository.AddAsync(keyboard);

    await _unitOfWork.SaveChangesAsync(); // Transaction handled automatically

    return Ok("Gaming bundle created successfully!");
}

// ❌ INCORRECT: Manual transaction management (not needed)
// await _unitOfWork.BeginTransactionAsync();
// await _unitOfWork.CommitTransactionAsync();
// await _unitOfWork.RollbackTransactionAsync();
```

This approach follows the principle that the Unit of Work should handle the complexity of transaction management internally, providing a simple interface for business logic.

## Generic Unit of Work Benefits

The generic implementation provides several advantages over hardcoded repository properties:

### 1. **Scalability**
Adding new entities requires no changes to the Unit of Work interface or implementation. The pattern automatically handles any entity type.

### 2. **Reduced Coupling**
The Unit of Work is not tightly coupled to specific repository types, making it more flexible and maintainable.

### 3. **Dynamic Repository Creation**
Repositories are created on-demand using a factory pattern, reducing memory usage and improving performance.

### 4. **Specialized Repository Support**
The implementation can return specialized repositories (like `ProductRepository`) when available, while falling back to generic repositories for other entities.

## Benefits of the Unit of Work Pattern

### 1. Transaction Management
The pattern ensures that complex business operations maintain data consistency. All related operations either succeed together or fail together, eliminating partial data states.

### 2. Single Responsibility Principle
Each repository focuses on its specific entity operations, while the Unit of Work coordinates the overall transaction management.

### 3. Improved Testability
The interface-based design makes mocking straightforward, significantly improving unit test development and maintenance.

### 4. Performance Optimization
Lazy loading ensures that repositories are only instantiated when needed, reducing memory overhead and improving application startup times.

## Common Challenges and Solutions

### Challenge 1: Circular Dependency Issues
Directly injecting repositories into the constructor can lead to circular dependency problems:

```csharp
// Problematic approach
public UnitOfWork(ApplicationDbContext context, IProductRepository products)
```

**Solution:** Use lazy loading pattern for repository instantiation, as demonstrated in the implementation above.

### Challenge 2: Resource Management
Improper disposal implementation can lead to memory leaks and resource exhaustion.

**Solution:** Implement proper IDisposable pattern and rely on the DI container's automatic disposal for scoped services.

### Challenge 3: Nested Transaction Scenarios
Starting transactions when one is already active can lead to runtime exceptions.

**Solution:** Add guard clauses in the implementation to check transaction state before beginning new transactions.

## When to Use the Unit of Work Pattern

**Use the pattern when:**
- Working with multiple related entities that must maintain consistency
- Complex business transactions require coordination across repositories
- You need explicit transaction control and rollback capabilities
- Building applications with complex domain logic

**Avoid the pattern when:**
- Dealing with simple CRUD operations on single entities
- Working in microservice architectures where each service owns its data
- Building read-only applications or simple APIs
- The added complexity doesn't justify the benefits

## Conclusion

The Unit of Work pattern provides a robust solution for managing complex business transactions and maintaining data consistency across multiple entities. While it introduces additional complexity compared to simple repository implementations, the benefits become significant when dealing with business scenarios that require coordination across multiple repositories.

The combination of Repository and Unit of Work patterns creates a solid foundation for data access layer architecture, particularly suitable for applications with complex domain logic and strict consistency requirements.

**Architectural Considerations:** In Clean Architecture implementations, define the Unit of Work interface in the Application layer and implement it in the Infrastructure layer. This approach maintains proper dependency inversion and separates business logic from data access concerns.

The pattern excels in scenarios requiring explicit transaction management, but it's important to evaluate whether the added complexity aligns with the specific requirements of your application. For simple CRUD applications, the traditional repository pattern alone may be sufficient.

---

*Note: The code examples provided are for demonstration purposes. Production implementations should include comprehensive error handling, logging, security considerations, and proper monitoring.*

**Implementation Note:** This article demonstrates Unit of Work combined with Repository pattern, which is a common best practice. While Unit of Work can be implemented independently, combining both patterns provides better separation of concerns and improved testability.