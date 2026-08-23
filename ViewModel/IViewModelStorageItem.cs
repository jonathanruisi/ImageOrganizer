using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Windows.Storage;

namespace ImageOrganizer.ViewModel
{
    public interface IViewModelStorageItem
    {
        /// <inheritdoc cref="IStorageItemProperties.FolderRelativeId"/>
        string Id { get; }

        /// <inheritdoc cref="IStorageItem.Name"/>
        string Name { get; }

        /// <inheritdoc cref="IStorageItem.Path"/>
        string Path { get; }

        /// <summary>
        /// Gets a value indicating whether the item is ready for immediate use.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Asynchronously loads the storage item.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous load operation. The task result is <see langword="true"/> if the
        /// load operation succeeds; otherwise, <see langword="false"/>.
        /// </returns>
        Task<bool> MakeReadyAsync();
    }
}