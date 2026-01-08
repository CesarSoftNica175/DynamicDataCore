using DynamicDataCore.Abstractions;
using DynamicDataCore.Common.Response;
using DynamicDataCore.Infraestructure.Implementation;
using Moq;

namespace DynamicDataCore.Tests
{

    // Test entity for generic service tests
    public class TestEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }

    }

    public class BaseGenericServiceTests
    {

        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<TestEntity>> _mockRepository;
        private readonly BaseGenericServiceImpl<TestEntity> _service;

        public BaseGenericServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRepository = new Mock<IGenericRepository<TestEntity>>();

            _mockUnitOfWork.Setup(uow => uow.Repository<TestEntity>())
                           .Returns(_mockRepository.Object);

            _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync())
                           .ReturnsAsync(OperationResult<bool>.Ok(true));

            _service = new BaseGenericServiceImpl<TestEntity>(_mockUnitOfWork.Object);
        }

        [Fact]
        public async Task AddAsync_ShouldReturnSuccess_WhenRepositorySucceeds()
        {
            var entity = new TestEntity { Id = 1, Name = "Test" };
            _mockRepository.Setup(r => r.AddAsync(entity))
                           .ReturnsAsync(OperationResult<bool>.Ok(true));

            var result = await _service.AddAsync(entity);

            Assert.True(result.Success);
            _mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task RetrieveByIdAsync_ShouldReturnEntity_WhenRepositoryReturnsEntity()
        {
            var entity = new TestEntity { Id = 1, Name = "Test" };
            _mockRepository.Setup(r => r.RetrieveByIdAsync(1, true))
                           .ReturnsAsync(OperationResult<TestEntity?>.Ok(entity));

            var result = await _service.RetrieveByIdAsync(1);

            Assert.True(result.Success);
            Assert.Equal("Test", result.Data?.Name);
        }

        [Fact]
        public async Task DeleteAsync_ShouldReturnFail_WhenEntityNotFound()
        {
            _mockRepository.Setup(r => r.RetrieveByIdAsync(1, false))
                           .ReturnsAsync(OperationResult<TestEntity?>.Fail("Not found"));

            var result = await _service.DeleteAsync(1);

            Assert.False(result.Success);
            Assert.Equal("Entity not found.", result.Message);
        }

    }

}