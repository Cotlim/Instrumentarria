using ReLogic.Content;
using ReLogic.Content.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Systems.AssetsManagers
{
    /// <summary>
    /// Base class for asset management systems that automatically discover and load files using Asset&lt;T&gt;.
    /// Provides common functionality for caching, loading, and managing assets.
    /// </summary>
    /// <typeparam name="TAsset">The type of asset to manage</typeparam>
    internal abstract class AssetManager<TAsset> : ModSystem where TAsset : class
    {
        // Dictionary for fast lookup: filename (without extension) -> Asset
        protected readonly Dictionary<string, Asset<TAsset>> _assets = new();
        
        /// <summary>
        /// Gets the folder path where assets are stored (e.g., "Assets/Midi/").
        /// </summary>
        protected abstract string AssetFolder { get; }
        
        /// <summary>
        /// Gets the file extension to search for (e.g., ".mid", ".sf2").
        /// </summary>
        protected abstract string FileExtension { get; }
        
        /// <summary>
        /// Gets the asset type name for logging (e.g., "MIDI", "SoundFont").
        /// </summary>
        protected abstract string AssetTypeName { get; }
        
        /// <summary>
        /// Gets the asset reader for this type.
        /// </summary>
        protected abstract IAssetReader AssetReader { get; }

        public override void Load()
        {
            // Find all files with the specified extension
            List<string> assetFiles = DiscoverAssetFiles();

            Log.Info($"Discovered {assetFiles.Count} {AssetTypeName} file(s) in {AssetFolder}");

            // Load each discovered file
            foreach (var assetPath in assetFiles)
            {
                LoadAsset(assetPath);
            }
        }

        public override void Unload()
        {
            // Dispose all assets and ensure streams are closed
            foreach (var kvp in _assets)
            {
                try
                {
                    var asset = kvp.Value;
                    
                    // Force asset to load if it hasn't yet (this closes the stream)
                    if (asset != null && asset.State != AssetState.NotLoaded)
                    {
                        // Wait for asset to finish loading
                        asset.Wait?.Invoke();
                    }
                    
                    // Dispose the asset
                    asset?.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Warn($"Error disposing {AssetTypeName} asset '{kvp.Key}': {ex.Message}");
                }
            }
            
            _assets.Clear();
            
            Log.Info($"{AssetTypeName}Assets unloaded successfully");
        }

        /// <summary>
        /// Gets an asset by name (without extension).
        /// </summary>
        /// <param name="fileName">Name of the file without extension</param>
        /// <returns>The asset, or null if not found</returns>
        protected Asset<TAsset> GetAsset(string fileName)
        {
            if (_assets.TryGetValue(fileName, out var asset))
            {
                return asset;
            }
            
            Log.Warn($"{AssetTypeName} asset '{fileName}' not found!");
            return null;
        }

        /// <summary>
        /// Gets all loaded asset names.
        /// </summary>
        protected IEnumerable<string> GetAllAssetNames() => _assets.Keys;

        /// <summary>
        /// Checks if an asset with the given name exists.
        /// </summary>
        protected bool HasAsset(string fileName) => _assets.ContainsKey(fileName);

        /// <summary>
        /// Discovers all files with the specified extension in the asset folder.
        /// </summary>
        private List<string> DiscoverAssetFiles()
        {
            var assetFiles = new List<string>();
            
            try
            {
                // Check if mod has files
                if (Mod.File == null)
                {
                    Log.Warn($"Mod.File is null - cannot discover {AssetTypeName} files");
                    return assetFiles;
                }
                
                // Enumerate all files with the specified extension
                var allFiles = Mod.File.Where(entry =>
                    entry.Name.StartsWith(AssetFolder, StringComparison.OrdinalIgnoreCase) && 
                    entry.Name.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase)
                );
                
                foreach (var file in allFiles)
                {
                    assetFiles.Add(file.Name);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error discovering {AssetTypeName} files: {ex.Message}");
            }
            
            return assetFiles;
        }

        /// <summary>
        /// Loads a single asset file.
        /// </summary>
        private void LoadAsset(string assetPath)
        {
            Stream stream = null;
            
            try
            {
                // Use shared stream (newFileStream: false) - must close after loading
                stream = Mod.GetFileStream(assetPath, newFileStream: false);
                var asset = CreateUntrackedWithReader(stream, assetPath);
                
                if (asset != null)
                {
                    // Asset is loaded synchronously due to ImmediateLoad mode
                    // Stream is now safe to close
                    stream?.Dispose();
                    stream = null;
                    
                    // Extract filename without extension for dictionary key
                    string fileName = Path.GetFileNameWithoutExtension(assetPath);
                    _assets[fileName] = asset;

                    // Log with custom info from derived class
                    LogAssetLoaded(fileName, asset);
                }
                else
                {
                    Log.Warn($"Failed to load {AssetTypeName} asset: {assetPath}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error loading {AssetTypeName} file '{assetPath}': {ex.Message}");
            }
            finally
            {
                // Ensure stream is always closed
                stream?.Dispose();
            }
        }

        /// <summary>
        /// Logs information about a loaded asset. Can be overridden for custom logging.
        /// </summary>
        protected virtual void LogAssetLoaded(string fileName, Asset<TAsset> asset)
        {
            Log.Info($"Loaded {AssetTypeName}: {fileName}");
        }

        /// <summary>
        /// Creates an untracked asset using the asset reader.
        /// </summary>
        private Asset<TAsset> CreateUntrackedWithReader(Stream stream, string name, AssetRequestMode mode = AssetRequestMode.ImmediateLoad)
        {
            if (Main.Assets is not AssetRepository assetRepository)
            {
                Log.Error("Main.Assets is not AssetRepository");
                return null;
            }

            var ext = Path.GetExtension(name);
            var asset = new Asset<TAsset>(name[..^ext.Length]);
            var loadTask = assetRepository.LoadUntracked(stream, AssetReader, asset, mode);
            asset.Wait = () => assetRepository.SafelyWaitForLoad(asset, loadTask, tracked: false);
            
            if (mode == AssetRequestMode.ImmediateLoad)
            {
                asset.Wait();
            }

            return asset;
        }
    }
}

