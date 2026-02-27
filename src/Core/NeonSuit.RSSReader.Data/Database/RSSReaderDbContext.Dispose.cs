// =======================================================
// Data/Database/RssReaderDbContext.Dispose.cs
// =======================================================

namespace NeonSuit.RSSReader.Data.Database
{
    internal partial class RSSReaderDbContext
    {
        /// <inheritdoc/>
        public override void Dispose()
        {
            if (!_isDisposed)
            {
                _logger?.Debug("Disposing DbContext");
                base.Dispose();
                _isDisposed = true;
                GC.SuppressFinalize(this);
            }
        }

        /// <inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            if (!_isDisposed)
            {
                _logger?.Debug("Disposing DbContext asynchronously");

                try
                {
                    await base.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger?.Error(ex, "Error disposing DbContext");
                    throw;
                }
                finally
                {
                    _isDisposed = true;
                    GC.SuppressFinalize(this);
                }
            }
        }
    }
}