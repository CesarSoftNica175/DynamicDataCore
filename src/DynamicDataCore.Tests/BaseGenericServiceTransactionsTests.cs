using DynamicDataCore.Abstractions;
using DynamicDataCore.Common.Response;
using DynamicDataCore.Infraestructure.Implementation;
using Moq;

namespace DynamicDataCore.Tests
{

    // Test entity for transaction tests
    public class TransactionEntity
    {
        public int Id { get; set; }

        public string? Description { get; set; }

    }

    public class BaseGenericServiceTransactionsTests
    {

        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<TransactionEntity>> _mockRepository;
        private readonly BaseGenericServiceImpl<TransactionEntity> _service;

        public BaseGenericServiceTransactionsTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRepository = new Mock<IGenericRepository<TransactionEntity>>();

            _mockUnitOfWork.Setup(uow => uow.Repository<TransactionEntity>())
                           .Returns(_mockRepository.Object);

            _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync())
                           .ReturnsAsync(OperationResult<bool>.Ok(true));

            _service = new BaseGenericServiceImpl<TransactionEntity>(_mockUnitOfWork.Object);
        }

        // Case 1: Trasaction begins successfully
        [Fact]
        public async Task BeginTransactionAsync_ShouldInvokeUnitOfWork_WhenCalled()
        {
            // Arrange
            _mockUnitOfWork.Setup(uow => uow.BeginTransactionAsync())
                           .Returns(Task.CompletedTask)
                           .Verifiable();

            // Act
            await _service.GetUnitOfWork().BeginTransactionAsync();

            // Assert
            _mockUnitOfWork.Verify(uow => uow.BeginTransactionAsync(), Times.Once);
        }

        // Case 2: Transaction commit successful
        [Fact]
        public async Task CommitTransactionAsync_ShouldCommit_WhenTransactionIsActive()
        {
            // Arrange
            _mockUnitOfWork.Setup(uow => uow.HasActiveTransaction).Returns(true);
            _mockUnitOfWork.Setup(uow => uow.CommitTransactionAsync())
                           .Returns(Task.CompletedTask)
                           .Verifiable();

            // Act
            await _service.GetUnitOfWork().CommitTransactionAsync();

            // Assert
            _mockUnitOfWork.Verify(uow => uow.CommitTransactionAsync(), Times.Once);
        }

        // Case 3: Transaction rollback successful
        [Fact]
        public async Task RollbackTransactionAsync_ShouldRollback_WhenTransactionIsActive()
        {
            // Arrange
            _mockUnitOfWork.Setup(uow => uow.HasActiveTransaction).Returns(true);
            _mockUnitOfWork.Setup(uow => uow.RollbackTransactionAsync())
                           .Returns(Task.CompletedTask)
                           .Verifiable();

            // Act
            await _service.GetUnitOfWork().RollbackTransactionAsync();

            // Assert
            _mockUnitOfWork.Verify(uow => uow.RollbackTransactionAsync(), Times.Once);
        }

        // Case 4: BeginTransaction throws when transaction already active
        [Fact]
        public async Task BeginTransactionAsync_ShouldThrow_WhenTransactionAlreadyActive()
        {
            // Arrange
            _mockUnitOfWork.Setup(uow => uow.HasActiveTransaction).Returns(true);
            _mockUnitOfWork.Setup(uow => uow.BeginTransactionAsync())
                           .ThrowsAsync(new InvalidOperationException("Ya existe una transacción activa."));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.GetUnitOfWork().BeginTransactionAsync());

            Assert.Equal("Ya existe una transacción activa.", ex.Message);
        }

        // Case 5: SaveChangesAsync does not call when transaction is active
        [Fact]
        public async Task SaveChangesAsync_ShouldNotCall_WhenTransactionActive()
        {
            // Arrange
            _mockUnitOfWork.Setup(uow => uow.HasActiveTransaction).Returns(true);

            // Act
            var result = await _service.GetUnitOfWork().SaveChangesAsync();

            // Assert
            Assert.True(result.Success);
            _mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(), Times.Once);
        }

        // Case 6: AddAsync commits transaction when operation succeeds
        [Fact]
        public async Task AddAsync_ShouldCommit_WhenOperationSucceeds()
        {
            // Arrange
            var entity = new TransactionEntity { Id = 1, Description = "New Item" };

            _mockRepository.Setup(r => r.AddAsync(entity))
                           .ReturnsAsync(OperationResult<bool>.Ok(true));

            _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync())
                           .ReturnsAsync(OperationResult<bool>.Ok(true))
                           .Verifiable();

            // Act
            var result = await _service.AddAsync(entity);

            // Assert
            Assert.True(result.Success);
            _mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(), Times.Once);
        }

    }
}
