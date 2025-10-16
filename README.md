# 🧩 DynamicDataCore Overview

# 

# DynamicDataCore is a lightweight and extensible data access framework built on top of Entity Framework Core.



# It provides a clean and unified abstraction for repositories, unit of work, and generic service layers, designed to support multi-context and transaction-safe operations in modern .NET applications.

# 

# This package is ideal for projects that aim to maintain a clean architecture, testability, and separation of concerns between data access, business logic, and application layers.

# 

# 🚀 Key Features

# 

# ✅ Generic Repository Pattern — CRUD and queryable access for any entity.

# 

# ✅ Unit of Work Pattern — Coordinated transactions across multiple repositories.

# 

# ✅ Base Service Layer — Simplified business logic integration.

# 

# ✅ Transaction Management — Explicit Begin, Commit, and Rollback control.

# 

# ✅ OperationResult<T> — Unified response model for success/error tracking.

# 

# ✅ Multi-DbContext Support — Seamless configuration for multiple database contexts.

# 

# ✅ Fully Testable — Supports mocking (Moq) and manual stubs for unit tests.





🏗️ Architecture Overview



Application Layer (Controllers / Services)

&nbsp;       ↓

Business Layer (BaseGenericServiceImpl<T>)

&nbsp;       ↓

Infrastructure Layer (UnitOfWorkImpl + GenericRepositoryImpl)

&nbsp;       ↓

Data Layer (EF Core DbContexts)



⚙️ Configuration Example (appsettings.json)



{

&nbsp; "ConnectionStrings": {

&nbsp;   "SqlServerConnection": "Server=.;Database=MyAppDb;Trusted\_Connection=True;",

&nbsp;   "PostgresConnection": "Host=localhost;Database=MyAppDb;Username=postgres;Password=admin;"

&nbsp; },

&nbsp; "DbContextMappings": {

&nbsp;   "Default": "DynamicDataCore.SqlServerDbContext",

&nbsp;   "Reporting": "DynamicDataCore.ReportingDbContext"

&nbsp; }

}



🧠 Usage Example – Basic Setup



using DynamicDataCore.Abstractions;

using DynamicDataCore.Implementation;



// Register services in Program.cs / Startup.cs

builder.Services.AddDbContext<AppDbContext>(options =>

&nbsp;   options.UseSqlServer(configuration.GetConnectionString("SqlServerConnection")));



builder.Services.AddScoped<IAppDbContext, AppDbContext>();

builder.Services.AddScoped<IUnitOfWork, UnitOfWorkImpl>();

builder.Services.AddScoped(typeof(IBaseGenericService<>), typeof(BaseGenericServiceImpl<>));



💡 Example 1: Basic CRUD Operations



public class ProductService

{

&nbsp;   private readonly IBaseGenericService<Product> \_productService;



&nbsp;   public ProductService(IBaseGenericService<Product> productService)

&nbsp;   {

&nbsp;       \_productService = productService;

&nbsp;   }



&nbsp;   public async Task AddProductAsync(Product product)

&nbsp;   {

&nbsp;       var result = await \_productService.AddAsync(product);

&nbsp;       if (!result.Success)

&nbsp;           throw new Exception(result.Message);

&nbsp;   }



&nbsp;   public async Task<IEnumerable<Product>> GetAllAsync()

&nbsp;   {

&nbsp;       var result = await \_productService.RetrieveAsync();

&nbsp;       return result.Data ?? Enumerable.Empty<Product>();

&nbsp;   }

}



💡 Example 2: Explicit Transaction Handling



public class OrderService

{

&nbsp;   private readonly IUnitOfWork \_unitOfWork;

&nbsp;   private readonly IBaseGenericService<Order> \_orderService;

&nbsp;   private readonly IBaseGenericService<OrderItem> \_itemService;



&nbsp;   public OrderService(IUnitOfWork unitOfWork)

&nbsp;   {

&nbsp;       \_unitOfWork = unitOfWork;

&nbsp;       \_orderService = new BaseGenericServiceImpl<Order>(\_unitOfWork);

&nbsp;       \_itemService = new BaseGenericServiceImpl<OrderItem>(\_unitOfWork);

&nbsp;   }



&nbsp;   public async Task<OperationResult<bool>> CreateOrderWithItemsAsync(Order order, IEnumerable<OrderItem> items)

&nbsp;   {

&nbsp;       try

&nbsp;       {

&nbsp;           await \_unitOfWork.BeginTransactionAsync();



&nbsp;           var orderResult = await \_orderService.AddAsync(order);

&nbsp;           if (!orderResult.Success)

&nbsp;               throw new Exception(orderResult.Message);



&nbsp;           foreach (var item in items)

&nbsp;           {

&nbsp;               item.OrderId = order.Id;

&nbsp;               var itemResult = await \_itemService.AddAsync(item);

&nbsp;               if (!itemResult.Success)

&nbsp;                   throw new Exception(itemResult.Message);

&nbsp;           }



&nbsp;           await \_unitOfWork.CommitTransactionAsync();

&nbsp;           return OperationResult<bool>.Ok(true);

&nbsp;       }

&nbsp;       catch (Exception ex)

&nbsp;       {

&nbsp;           await \_unitOfWork.RollbackTransactionAsync();

&nbsp;           return OperationResult<bool>.Fail("Transaction rolled back.", ex);

&nbsp;       }

&nbsp;   }

}



🧪 Example 3: Unit Tests (Moq)



public class BaseGenericServiceTests

{

&nbsp;   private readonly Mock<IUnitOfWork> \_mockUow = new();

&nbsp;   private readonly Mock<IGenericRepository<Product>> \_mockRepo = new();

&nbsp;   private readonly BaseGenericServiceImpl<Product> \_service;



&nbsp;   public BaseGenericServiceTests()

&nbsp;   {

&nbsp;       \_mockUow.Setup(u => u.Repository<Product>()).Returns(\_mockRepo.Object);

&nbsp;       \_mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(OperationResult<bool>.Ok(true));

&nbsp;       \_service = new BaseGenericServiceImpl<Product>(\_mockUow.Object);

&nbsp;   }



&nbsp;   \[Fact]

&nbsp;   public async Task AddAsync\_Should\_SaveChanges\_When\_Success()

&nbsp;   {

&nbsp;       var product = new Product { Id = 1, Name = "Test" };

&nbsp;       \_mockRepo.Setup(r => r.AddAsync(product)).ReturnsAsync(OperationResult<bool>.Ok(true));



&nbsp;       var result = await \_service.AddAsync(product);



&nbsp;       Assert.True(result.Success);

&nbsp;       \_mockUow.Verify(u => u.SaveChangesAsync(), Times.Once);

&nbsp;   }

}



🧱 Entities Example



public class Order

{

&nbsp;   public int Id { get; set; }

&nbsp;   public DateTime Date { get; set; }

&nbsp;   public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

}



public class OrderItem

{

&nbsp;   public int Id { get; set; }

&nbsp;   public int OrderId { get; set; }

&nbsp;   public string ProductName { get; set; } = string.Empty;

}



📦 Package Summary



| Layer          | Interface / Class           | Description                                              |

| -------------- | --------------------------- | -------------------------------------------------------- |

| Abstractions   | `IAppDbContext`             | Defines a lightweight abstraction over EF Core DbContext |

| Abstractions   | `IUnitOfWork`               | Encapsulates transaction and repository coordination     |

| Abstractions   | `IGenericRepository<T>`     | Generic repository contract for CRUD operations          |

| Implementation | `UnitOfWorkImpl`            | Default EF Core implementation of `IUnitOfWork`          |

| Implementation | `GenericRepositoryImpl<T>`  | Default EF Core implementation of repository             |

| Implementation | `BaseGenericServiceImpl<T>` | Generic business service using UnitOfWork                |

| Common         | `OperationResult<T>`        | Unified response object for error/success handling       |



🧾 License



This project is licensed under the MIT License — you are free to use, modify, and distribute under the same terms.











