using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Controllers;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using JsonApiDotNetCoreExample.Models;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Models;
using System.Threading.Tasks;
using System.Linq;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Builders;
using JsonApiDotNetCore.Managers.Contracts;

namespace UnitTests.Data
{
    public class DefaultResourceRepositoryTests : JsonApiControllerMixin
    {
        private readonly Mock<DbSet<TodoItem>> _dbSetMock;
        private readonly Mock<DbContext> _contextMock;
        private readonly Mock<ITargetedFields> _targetedFieldsMock;
        private readonly Mock<IDbContextResolver> _contextResolverMock;
        private readonly TodoItem _todoItem;

        public DefaultResourceRepositoryTests()
        {
            _todoItem = new TodoItem
            {
                Id = 1,
                Description = Guid.NewGuid().ToString(),
                Ordinal = 10
            };
            _dbSetMock = DbSetMock.Create(new[] { _todoItem });
            _contextMock = new Mock<DbContext>();
            _contextResolverMock = new Mock<IDbContextResolver>();
            _targetedFieldsMock = new Mock<ITargetedFields>();
        }

        [Fact]
        public async Task UpdateAsync_Updates_Attributes_In_AttributesToUpdate()
        {
            // arrange
            var todoItemUpdates = new TodoItem
            {
                Id = _todoItem.Id,
                Description = Guid.NewGuid().ToString()
            };

            var descAttr = new AttrAttribute("description", "Description")
            {
                PropertyInfo = typeof(TodoItem).GetProperty(nameof(TodoItem.Description))
            };
            _targetedFieldsMock.Setup(m => m.Attributes).Returns(new List<AttrAttribute> { descAttr });
            _targetedFieldsMock.Setup(m => m.Relationships).Returns(new List<RelationshipAttribute>());

            var repository = GetRepository();

            // act
            var updatedItem = await repository.UpdateAsync(todoItemUpdates);

            // assert
            Assert.NotNull(updatedItem);
            Assert.Equal(_todoItem.Ordinal, updatedItem.Ordinal);
            Assert.Equal(todoItemUpdates.Description, updatedItem.Description);
        }

        private DefaultResourceRepository<TodoItem> GetRepository()
        {

            _contextMock
                .Setup(m => m.Set<TodoItem>())
                .Returns(_dbSetMock.Object);

            _contextResolverMock
                .Setup(m => m.GetContext())
                .Returns(_contextMock.Object);

            var resourceGraph = new ResourceGraphBuilder().AddResource<TodoItem>().Build();


            return new DefaultResourceRepository<TodoItem>(
                _targetedFieldsMock.Object,
                _contextResolverMock.Object,
                resourceGraph, null, null);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-10)]
        public async Task Page_When_PageSize_Is_NonPositive_Does_Nothing(int pageSize)
        {
            var todoItems = DbSetMock.Create(TodoItems(2, 3, 1)).Object;
            var repository = GetRepository();

            var result = await repository.PageAsync(todoItems, pageSize, 3);

            Assert.Equal(TodoItems(2, 3, 1), result, new IdComparer<TodoItem>());
        }

        [Fact]
        public async Task Page_When_PageNumber_Is_Zero_Pretends_PageNumber_Is_One()
        {
            var todoItems = DbSetMock.Create(TodoItems(2, 3, 1)).Object;
            var repository = GetRepository();

            var result = await repository.PageAsync(todoItems, 1, 0);

            Assert.Equal(TodoItems(2), result, new IdComparer<TodoItem>());
        }

        [Fact]
        public async Task Page_When_PageNumber_Of_PageSize_Does_Not_Exist_Return_Empty_Queryable()
        {
            var todoItems = DbSetMock.Create(TodoItems(2, 3, 1)).Object;
            var repository = GetRepository();

            var result = await repository.PageAsync(todoItems, 2, 3);

            Assert.Empty(result);
        }

        [Theory]
        [InlineData(3, 2, new[] { 4, 5, 6 })]
        [InlineData(8, 2, new[] { 9 })]
        [InlineData(20, 1, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })]
        public async Task Page_When_PageNumber_Is_Positive_Returns_PageNumberTh_Page_Of_Size_PageSize(int pageSize, int pageNumber, int[] expectedResult)
        {
            var todoItems = DbSetMock.Create(TodoItems(1, 2, 3, 4, 5, 6, 7, 8, 9)).Object;
            var repository = GetRepository();

            var result = await repository.PageAsync(todoItems, pageSize, pageNumber);

            Assert.Equal(TodoItems(expectedResult), result, new IdComparer<TodoItem>());
        }



        private static TodoItem[] TodoItems(params int[] ids)
        {
            return ids.Select(id => new TodoItem { Id = id }).ToArray();
        }

        private class IdComparer<T> : IEqualityComparer<T>
            where T : IIdentifiable
        {
            public bool Equals(T x, T y) => x?.StringId == y?.StringId;

            public int GetHashCode(T obj) => obj?.StringId?.GetHashCode() ?? 0;
        }
    }
}
