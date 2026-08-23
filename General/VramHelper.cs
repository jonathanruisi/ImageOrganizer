using System;

using Vortice.DXGI;

namespace ImageOrganizer
{
    internal sealed record VideoMemoryInfo(ulong BudgetBytes,
                                           ulong CurrentUsageBytes,
                                           ulong AvailableForReservationBytes,
                                           ulong CurrentReservationBytes,
                                           string AdapterDescription);

    internal static class VramHelper
    {
        public static VideoMemoryInfo? GetVideoMemoryInfoForPrimaryAdapter()
        {
            try
            {
                // Create DXGI factory and enumerate first adapter
                using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
                var result = factory.EnumAdapters1(0, out IDXGIAdapter1 adapter1);
                if (adapter1.NativePointer == IntPtr.Zero)
                    return null;

                // Query adapter3 (needed for QueryVideoMemoryInfo)
                using var adapter3 = adapter1.QueryInterfaceOrNull<IDXGIAdapter3>();
                if (adapter3 is null)
                    return null;

                // Use Local segment (video memory on the GPU). System segment exists on some systems.
                var info = adapter3.QueryVideoMemoryInfo(0, MemorySegmentGroup.Local);

                // Adapter description for display (optional)
                var desc = adapter1.Description1;
                string adapterName = desc.Description.TrimEnd('\0');

                return new VideoMemoryInfo(
                    BudgetBytes: info.Budget,
                    CurrentUsageBytes: info.CurrentUsage,
                    AvailableForReservationBytes: info.AvailableForReservation,
                    CurrentReservationBytes: info.CurrentReservation,
                    AdapterDescription: adapterName);
            }
            catch
            {
                return null;
            }
        }
    }
}