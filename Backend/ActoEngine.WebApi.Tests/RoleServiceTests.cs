using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;
using ActoEngine.WebApi.Services.RoleService;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ActoEngine.WebApi.Tests.Services
{
    public class RoleServiceTests
    {
        private readonly Mock<IRoleRepository> _mockRoleRepository;
        private readonly Mock<IPermissionRepository> _mockPermissionRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<ILogger<RoleService>> _mockLogger;
        private readonly RoleService _roleService;

        public RoleServiceTests()
        {
            _mockRoleRepository = new Mock<IRoleRepository>();
            _mockPermissionRepository = new Mock<IPermissionRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockLogger = new Mock<ILogger<RoleService>>();

            _roleService = new RoleService(
                _mockRoleRepository.Object,
                _mockPermissionRepository.Object,
                _mockUserRepository.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task DeleteRoleAsync_ShouldRemoveRoleFromUsers_BeforeDeletingRole()
        {
            // Arrange
            int roleId = 123;
            var role = new Role { RoleId = roleId, RoleName = "Test Role", IsSystem = false };

            _mockRoleRepository.Setup(r => r.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(role);

            // Act
            await _roleService.DeleteRoleAsync(roleId);

            // Assert
            // 1. Verify RemoveRoleFromUsersAsync was called
            _mockUserRepository.Verify(u => u.RemoveRoleFromUsersAsync(roleId, It.IsAny<CancellationToken>()), Times.Once);

            // 2. Verify DeleteAsync was called
            _mockRoleRepository.Verify(r => r.DeleteAsync(roleId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteRoleAsync_ShouldThrow_WhenRoleIsSystem()
        {
            // Arrange
            int roleId = 1;
            var role = new Role { RoleId = roleId, RoleName = "Admin", IsSystem = true };

            _mockRoleRepository.Setup(r => r.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(role);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _roleService.DeleteRoleAsync(roleId));

            // Verify Delete was NOT called
            _mockRoleRepository.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockUserRepository.Verify(u => u.RemoveRoleFromUsersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
