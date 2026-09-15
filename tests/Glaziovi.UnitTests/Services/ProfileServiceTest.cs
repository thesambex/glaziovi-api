using Glaziovi.Core.Database;
using Glaziovi.Core.Providers;
using Glaziovi.Core.Providers.Identity;
using Glaziovi.Infrastructure.Services;
using Glaziovi.Modules.Iam.Domain;
using Glaziovi.Modules.Iam.Repositories;
using Glaziovi.Modules.Persons;
using Glaziovi.Modules.Persons.Domain;
using Glaziovi.Modules.Persons.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Glaziovi.UnitTests.Services;

public sealed class ProfileServiceTest
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IPersonProfileRepository> _personProfileRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IIdentityProvider> _identityProvider = new();
    private readonly Mock<ITransaction> _transaction = new();
    private readonly ProfileService _profileService;

    public ProfileServiceTest()
    {
        _unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transaction.Object);

        _profileService = new ProfileService(
            _userRepository.Object,
            _personProfileRepository.Object,
            _unitOfWork.Object,
            _identityProvider.Object,
            NullLogger<ProfileService>.Instance
        );
    }

    [Fact]
    public async Task CreateProfileAsync_WhenProvisioningSucceeds_PersistsProfileAndCommits()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var ct = cancellation.Token;
        var request = CreateRequest();
        User? savedUser = null;
        PersonProfile? savedProfile = null;

        _identityProvider
            .Setup(x => x.ProvisionUserAsync(It.IsAny<IdentityProvisionUser>(), ct))
            .ReturnsAsync(new ProvisionUserResult(ProvisionUserStatus.Success, "identity-user-id"));

        _userRepository
            .Setup(x => x.AddAsync(It.IsAny<User>(), ct))
            .Callback<User, CancellationToken>((user, _) => savedUser = user)
            .Returns(Task.CompletedTask);

        _personProfileRepository
            .Setup(x => x.AddAsync(It.IsAny<PersonProfile>(), ct))
            .Callback<PersonProfile, CancellationToken>((profile, _) => savedProfile = profile)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _profileService.CreateProfileAsync(request, ct);

        // Assert
        Assert.Equal(CreateProfileStatus.Success, result.Status);
        Assert.NotNull(savedUser);
        Assert.Equal("identity-user-id", savedUser.ExternalSubject);
        Assert.NotNull(savedProfile);
        Assert.Equal(savedUser.Id, savedProfile.UserId);
        Assert.Equal(request.FirstName, savedProfile.FirstName);
        Assert.Equal(request.LastName, savedProfile.LastName);
        Assert.Null(savedProfile.BirthDate);
        Assert.NotEqual(Guid.Empty, savedProfile.ExternalId);
        Assert.Equal(savedProfile.ExternalId, result.Id);

        _identityProvider.Verify(
            x => x.ProvisionUserAsync(
                new IdentityProvisionUser(
                    request.FirstName,
                    request.LastName,
                    request.Email,
                    request.Username,
                    request.Password
                ),
                ct
            ),
            Times.Once
        );
        _unitOfWork.Verify(x => x.BeginTransactionAsync(ct), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(ct), Times.Once);
        _transaction.Verify(x => x.CommitAsync(ct), Times.Once);
        _transaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        _transaction.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Theory]
    [InlineData(ProvisionUserStatus.Conflict, CreateProfileStatus.Conflict)]
    [InlineData(ProvisionUserStatus.BadRequest, CreateProfileStatus.BadRequest)]
    [InlineData(ProvisionUserStatus.Failure, CreateProfileStatus.Failure)]
    public async Task CreateProfileAsync_WhenProvisioningFails_RollsBackWithoutPersisting(
        ProvisionUserStatus provisionStatus,
        CreateProfileStatus expectedStatus
    )
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var ct = cancellation.Token;
        var request = CreateRequest();

        _identityProvider
            .Setup(x => x.ProvisionUserAsync(It.IsAny<IdentityProvisionUser>(), ct))
            .ReturnsAsync(new ProvisionUserResult(provisionStatus, null));

        // Act
        var result = await _profileService.CreateProfileAsync(request, ct);

        // Assert
        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.Id);
        _userRepository.Verify(
            x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _personProfileRepository.Verify(
            x => x.AddAsync(It.IsAny<PersonProfile>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        VerifyRollback(ct);
    }

    [Theory]
    [InlineData("provision")]
    [InlineData("user")]
    [InlineData("profile")]
    [InlineData("save")]
    [InlineData("commit")]
    public async Task CreateProfileAsync_WhenOperationThrows_ReturnsFailureAndRollsBack(
        string failingOperation
    )
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var ct = cancellation.Token;
        var request = CreateRequest();
        var exception = new InvalidOperationException("Operation failed.");

        _identityProvider
            .Setup(x => x.ProvisionUserAsync(It.IsAny<IdentityProvisionUser>(), ct))
            .ReturnsAsync(new ProvisionUserResult(ProvisionUserStatus.Success, "identity-user-id"));

        switch (failingOperation)
        {
            case "provision":
                _identityProvider
                    .Setup(x => x.ProvisionUserAsync(It.IsAny<IdentityProvisionUser>(), ct))
                    .ThrowsAsync(exception);
                break;
            case "user":
                _userRepository
                    .Setup(x => x.AddAsync(It.IsAny<User>(), ct))
                    .ThrowsAsync(exception);
                break;
            case "profile":
                _personProfileRepository
                    .Setup(x => x.AddAsync(It.IsAny<PersonProfile>(), ct))
                    .ThrowsAsync(exception);
                break;
            case "save":
                _unitOfWork.Setup(x => x.SaveChangesAsync(ct)).ThrowsAsync(exception);
                break;
            case "commit":
                _transaction.Setup(x => x.CommitAsync(ct)).ThrowsAsync(exception);
                break;
        }

        // Act
        var result = await _profileService.CreateProfileAsync(request, ct);

        // Assert
        Assert.Equal(CreateProfileStatus.Failure, result.Status);
        Assert.Null(result.Id);
        _transaction.Verify(x => x.RollbackAsync(ct), Times.Once);
        _transaction.Verify(x => x.DisposeAsync(), Times.Once);
        _transaction.Verify(
            x => x.CommitAsync(ct),
            failingOperation == "commit" ? Times.Once() : Times.Never()
        );
    }

    [Fact]
    public async Task CreateProfileAsync_WhenTransactionCannotBegin_PropagatesException()
    {
        // Arrange
        var request = CreateRequest();
        var exception = new InvalidOperationException("Transaction unavailable.");

        _unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _profileService.CreateProfileAsync(request, CancellationToken.None)
        );

        // Assert
        Assert.Same(exception, actual);
        _identityProvider.VerifyNoOtherCalls();
        _userRepository.VerifyNoOtherCalls();
        _personProfileRepository.VerifyNoOtherCalls();
        _transaction.VerifyNoOtherCalls();
    }

    private void VerifyRollback(CancellationToken ct)
    {
        _transaction.Verify(x => x.RollbackAsync(ct), Times.Once);
        _transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _transaction.Verify(x => x.DisposeAsync(), Times.Once);
    }

    private static CreateProfile CreateRequest() => new(
        "Ana",
        "Silva",
        "ana@example.com",
        "ana.silva",
        "test-password"
    );
}
