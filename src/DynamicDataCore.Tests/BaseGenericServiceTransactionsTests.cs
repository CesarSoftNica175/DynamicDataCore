using DynamicDataCore.Abstractions;
using DynamicDataCore.Common.Response;
using DynamicDataCore.Infrastructure.Implementation;
using Moq;

namespace DynamicDataCore.Tests;

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

        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(OperationResult<bool>.Ok(true));

        _service = new BaseGenericServiceImpl<TransactionEntity>(_mockUnitOfWork.Object);
    }

    [Fact]
    public async Task BeginTransactionAsync_ShouldInvokeUnitOfWork_WhenCalled()
    {
        _mockUnitOfWork.Setup(uow => uow.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask)
                       .Verifiable();

        await _service.GetUnitOfWork().BeginTransactionAsync();

        _mockUnitOfWork.Verify(uow => uow.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitTransactionAsync_ShouldCommit_WhenTransactionIsActive()
    {
        _mockUnitOfWork.Setup(uow => uow.HasActiveTransaction).Returns(true);
        _mockUnitOfWork.Setup(uow => uow.CommitTransactionAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask)
                       .Verifiable();

        await _service.GetUnitOfWork().CommitTransactionAsync();

        _mockUnitOfWork.Verify(uow => uow.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RollbackTransactionAsync_ShouldRollback_WhenTransactionIsActive()
    {
        _mockUnitOfWork.Setup(uow => uow.HasActiveTransaction).Returns(true);
        _mockUnitOfWork.Setup(uow => uow.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask)
                       .Verifiable();

        await _service.GetUnitOfWork().RollbackTransactionAsync();

        _mockUnitOfWork.Verify(uow => uow.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BeginTransactionAsync_ShouldThrow_WhenTransactionAlreadyActive()
    {
        _mockUnitOfWork.Setup(uow => uow.HasActiveTransaction).Returns(true);
        _mockUnitOfWork.Setup(uow => uow.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new InvalidOperationException("An active transaction already exists."));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetUnitOfWork().BeginTransactionAsync());

        Assert.Equal("An active transaction already exists.", ex.Message);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldReturnOk_WhenTransactionActive()
    {
        _mockUnitOfWork.Setup(uow => uow.HasActiveTransaction).Returns(true);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(OperationResult<bool>.Ok(true));

        var result = await _service.GetUnitOfWork().SaveChangesAsync();

        Assert.True(result.Success);
        _mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAsync_ShouldCommit_WhenOperationSucceeds()
    {
        var entity = new TransactionEntity { Id = 1, Description = "New Item" };

        _mockRepository.Setup(r => r.AddAsync(entity, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(OperationResult<bool>.Ok(true));

        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(OperationResult<bool>.Ok(true))
                       .Verifiable();

        var result = await _service.AddAsync(entity);

        Assert.True(result.Success);
        _mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
