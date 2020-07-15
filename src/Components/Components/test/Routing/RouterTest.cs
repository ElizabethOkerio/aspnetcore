// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Test.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Components.Test.Routing
{
    public class RouterTest
    {
        private readonly Mock<Router> _router;
        private readonly TestRenderer _renderer;

        public RouterTest()
        {
            _router = CreateMockRouter();
            _renderer = new TestRenderer();
            _renderer.AssignRootComponentId(_router.Object);
        }

        [Fact]
        public async Task CanRunOnNavigateAsync()
        {
            // Arrange
            var called = false;
            async Task OnNavigateAsync(NavigationContext args)
            {
                await Task.CompletedTask;
                called = true;
            }
            _router.Object.OnNavigateAsync = new EventCallbackFactory().Create<NavigationContext>(_router, OnNavigateAsync);

            // Act
            await _router.Object.RunOnNavigateWithRefreshAsync("http://example.com/jan", false);

            // Assert
            Assert.True(called);
        }

        [Fact]
        public async Task CanHandleSingleFailedOnNavigateAsync()
        {
            // Arrange
            async Task OnNavigateAsync(NavigationContext args)
            {
                await Task.CompletedTask;
                throw new Exception("This is an uncaught exception.");
            }
            _router.Object.OnNavigateAsync = new EventCallbackFactory().Create<NavigationContext>(_router, OnNavigateAsync);

            // Act
            await _router.Object.RunOnNavigateWithRefreshAsync("http://example.com/jan", false);

            // Assert
            _router.Verify(x => x.Refresh(false), Times.Never());
            Assert.Single(_renderer.HandledExceptions);
            var unhandledException = _renderer.HandledExceptions[0];
            Assert.Equal("This is an uncaught exception.", unhandledException.Message);
        }

        [Fact]
        public async Task CanceledFailedOnNavigateAsyncDoesNothing()
        {
            // Arrange
            async Task OnNavigateAsync(NavigationContext args)
            {
                if (args.Path.EndsWith("jan"))
                {
                    await Task.Delay(Timeout.Infinite);
                    throw new Exception("This is an uncaught exception.");
                }
            }
            _router.Object.OnNavigateAsync = new EventCallbackFactory().Create<NavigationContext>(_router, OnNavigateAsync);

            // Act
            var janTask = _router.Object.RunOnNavigateWithRefreshAsync("jan", false);
            var febTask = _router.Object.RunOnNavigateWithRefreshAsync("feb", false);

            await janTask;
            await febTask;

            // Assert that we render the second route component and don't throw an exception
            _router.Verify(x => x.Refresh(false), Times.Once());
            Assert.Empty(_renderer.HandledExceptions);
        }

        [Fact]
        public async Task CanHandleSingleCancelledOnNavigateAsync()
        {
            // Arrange
            async Task OnNavigateAsync(NavigationContext args)
            {
                var tcs = new TaskCompletionSource<int>();
                tcs.TrySetCanceled();
                await tcs.Task;
            }
            _router.Object.OnNavigateAsync = new EventCallbackFactory().Create<NavigationContext>(_router, OnNavigateAsync);

            // Act
            var janTask = _router.Object.RunOnNavigateWithRefreshAsync("http://example.com/jan", false);

            var janTaskException = await Assert.ThrowsAsync<InvalidOperationException>(() => janTask);

            // Assert
            Assert.Equal("OnNavigateAsync callback cannot be canceled.", janTaskException.Message);
            _router.Verify(x => x.Refresh(false), Times.Never());
        }

        [Fact]
        public async Task AlreadyCanceledOnNavigateAsyncDoesNothing()
        {
            // Arrange
            async Task OnNavigateAsync(NavigationContext args)
            {
                if (args.Path.EndsWith("jan"))
                {
                    var tcs = new TaskCompletionSource();
                    await Task.Delay(4000);
                    tcs.TrySetCanceled();
                    await tcs.Task;
                }
            }
            _router.Object.OnNavigateAsync = new EventCallbackFactory().Create<NavigationContext>(_router, OnNavigateAsync);

            // Act (start the operations then await them)
            var janTask = _router.Object.RunOnNavigateWithRefreshAsync("http://example.com/jan", false);
            var febTask = _router.Object.RunOnNavigateWithRefreshAsync("http://example.com/feb", false);

            await janTask;
            await febTask;

            // Assert
            _router.Verify(x => x.Refresh(false), Times.Once());
        }

        [Fact]
        public async Task CanCancelPreviousOnNavigateAsync()
        {
            // Arrange
            var cancelled = "";
            async Task OnNavigateAsync(NavigationContext args)
            {
                await Task.CompletedTask;
                args.CancellationToken.Register(() => cancelled = args.Path);
            };
            _router.Object.OnNavigateAsync = new EventCallbackFactory().Create<NavigationContext>(_router, OnNavigateAsync);

            // Act
            await _router.Object.RunOnNavigateWithRefreshAsync("jan", false);
            await _router.Object.RunOnNavigateWithRefreshAsync("feb", false);

            // Assert
            var expected = "jan";
            Assert.Equal(expected, cancelled);
        }

        [Fact]
        public async Task RefreshesOnceOnCancelledOnNavigateAsync()
        {
            // Arrange
            async Task OnNavigateAsync(NavigationContext args)
            {
                if (args.Path.EndsWith("jan"))
                {
                    await Task.Delay(Timeout.Infinite);
                }
            };
            _router.Object.OnNavigateAsync = new EventCallbackFactory().Create<NavigationContext>(_router, OnNavigateAsync);

            // Act
            var janTask = _router.Object.RunOnNavigateWithRefreshAsync("jan", false);
            var febTask = _router.Object.RunOnNavigateWithRefreshAsync("feb", false);

            await janTask;
            await febTask;

            // Assert refresh should've only been called once for the second route
            _router.Verify(x => x.Refresh(false), Times.Once());
        }

        private Mock<Router> CreateMockRouter()
        {
            var _router = new Mock<Router>() { CallBase = true };
            _router.Object.LoggerFactory = NullLoggerFactory.Instance;
            _router.Object.NavigationManager = new TestNavigationManager();
            _router.Setup(x => x.Refresh(It.IsAny<bool>())).Verifiable();
            return _router;
        }

        internal class TestNavigationManager : NavigationManager
        {
            public TestNavigationManager() =>
                Initialize("https://www.example.com/subdir/", "https://www.example.com/subdir/jan");

            protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotImplementedException();
        }
    }
}
