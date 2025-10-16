using DynamicDataCore.Abstractions;
using DynamicDataCore.Common.Response;
using DynamicDataCore.Implementation;
using Moq;

namespace DynamicDataCore.Tests
{

    // Entidad de prueba
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

        // Caso 1: Transacción inicia correctamente
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

        // Caso 2: Commit de transacción exitoso
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

        // Caso 3: Rollback de transacción exitoso
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

        // Caso 4: No ejecutar transacción si ya hay una activa
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

        // Caso 5: Confirmar que SaveChanges no se llama dentro de transacción activa
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

        // Caso 6: Commit después de operación AddAsync
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
