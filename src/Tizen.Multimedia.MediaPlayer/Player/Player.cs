/*
 * Copyright (c) 2016 Samsung Electronics Co., Ltd All Rights Reserved
 *
 * Licensed under the Apache License, Version 2.0 (the License);
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an AS IS BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Threading;
using static Interop;

namespace Tizen.Multimedia
{
    static internal class PlayerLog
    {
        internal const string Tag = "Tizen.Multimedia.Player";
        internal const string Enter = "[ENTER]";
        internal const string Leave = "[LEAVE]";
    }

    /// <summary>
    /// Provides the ability to control media playback.
    /// </summary>
    /// <remarks>
    /// The Player provides functions to play a media content.
    /// It also provides functions to adjust the configurations of the player such as playback rate, volume, looping etc.
    /// Note that only one video player can be played at one time.
    /// </remarks>
    public partial class Player : IDisposable, IDisplayable<PlayerErrorCode>
    {
        private PlayerHandle _handle;

        /// <summary>
        /// Initialize a new instance of the Player class.
        /// </summary>
        public Player()
        {
            NativePlayer.Create(out _handle).ThrowIfFailed("Failed to create player");

            Debug.Assert(_handle != null);

            RetrieveProperties();

            if (Features.IsSupported(Features.AudioEffect))
            {
                _audioEffect = new AudioEffect(this);
            }

            if (Features.IsSupported(Features.RawVideo))
            {
                RegisterVideoFrameDecodedCallback();
            }

            DisplaySettings = PlayerDisplaySettings.Create(this);
        }

        internal void ValidatePlayerState(params PlayerState[] desiredStates)
        {
            Debug.Assert(desiredStates.Length > 0);

            ValidateNotDisposed();

            var curState = State;
            if (curState.IsAnyOf(desiredStates))
            {
                return;
            }

            throw new InvalidOperationException($"The player is not in a valid state. " +
                $"Current State : { curState }, Valid State : { string.Join(", ", desiredStates) }.");
        }

        #region Dispose support
        private bool _disposed;

        /// <summary>
        /// Releases all resources used by the current instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                ReplaceDisplay(null);

                if (_source != null)
                {
                    try
                    {
                        _source.DetachFrom(this);
                    }
                    catch (Exception e)
                    {
                        Log.Error(PlayerLog.Tag, e.ToString());
                    }
                }
                _source = null;

                if (_handle != null)
                {
                    _handle.Dispose();
                }
                _disposed = true;
            }
        }

        internal void ValidateNotDisposed()
        {
            if (_disposed)
            {
                Log.Warn(PlayerLog.Tag, "player was disposed");
                throw new ObjectDisposedException(nameof(Player));
            }
        }

        internal bool IsDisposed => _disposed;
        #endregion

        #region Methods

        /// <summary>
        /// Gets the streaming download Progress.
        /// </summary>
        /// <returns>The <see cref="DownloadProgress"/> containing current download progress.</returns>
        /// <remarks>The player must be in the <see cref="PlayerState.Playing"/> or <see cref="PlayerState.Paused"/> state.</remarks>
        /// <exception cref="InvalidOperationException">
        ///     The player is not streaming.\n
        ///     -or-\n
        ///     The player is not in the valid state.
        ///     </exception>
        /// <exception cref="ObjectDisposedException">The player has already been disposed of.</exception>
        public DownloadProgress GetDownloadProgress()
        {
            Log.Debug(PlayerLog.Tag, PlayerLog.Enter);
            ValidatePlayerState(PlayerState.Playing, PlayerState.Paused);

            int start = 0;
            int current = 0;
            NativePlayer.GetStreamingDownloadProgress(Handle, out start, out current).
                ThrowIfFailed("Failed to get download progress");

            Log.Info(PlayerLog.Tag, "get download progress : " + start + ", " + current);

            return new DownloadProgress(start, current);
        }

        /// <summary>
        /// Sets the subtitle path for playback.
        /// </summary>
        /// <remarks>Only MicroDVD/SubViewer(*.sub), SAMI(*.smi), and SubRip(*.srt) subtitle formats are supported.
        ///     <para>The mediastorage privilege(http://tizen.org/privilege/mediastorage) must be added if any files are used to play located in the internal storage.
        ///     The externalstorage privilege(http://tizen.org/privilege/externalstorage) must be added if any files are used to play located in the external storage.</para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The player has already been disposed of.</exception>
        /// <exception cref="ArgumentException"><paramref name="path"/> is an empty string.</exception>
        /// <exception cref="FileNotFoundException">The specified path does not exist.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        public void SetSubtitle(string path)
        {
            Log.Debug(PlayerLog.Tag, PlayerLog.Enter);
            ValidateNotDisposed();

            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (path.Length == 0)
            {
                throw new ArgumentException("The path is empty.", nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"The specified file does not exist.", path);
            }

            NativePlayer.SetSubtitlePath(Handle, path).
                ThrowIfFailed("Failed to set the subtitle path to the player");

            Log.Debug(PlayerLog.Tag, PlayerLog.Leave);
        }

        /// <summary>
        /// Removes the subtitle path.
        /// </summary>
        /// <remarks>The player must be in the <see cref="PlayerState.Idle"/> state.</remarks>
        /// <exception cref="ObjectDisposedException">The player has already been disposed of.</exception>
        /// <exception cref="InvalidOperationException">The player is not in the valid state.</exception>
        public void ClearSubtitle()
        {
            Log.Debug(PlayerLog.Tag, PlayerLog.Enter);
            ValidatePlayerState(PlayerState.Idle);

            NativePlayer.SetSubtitlePath(Handle, null).
                ThrowIfFailed("Failed to clear the subtitle of the player");
            Log.Debug(PlayerLog.Tag, PlayerLog.Leave);
        }

        /// <summary>
        /// Sets the offset for the subtitle.
        /// </summary>
        /// <param name="offset">The value indicating a desired offset in milliseconds.</param>
        /// <remarks>The player must be in the <see cref="PlayerState.Playing"/> or <see cref="PlayerState.Paused"/> state.</remarks>
        /// <exception cref="ObjectDisposedException">The player has already been disposed of.</exception>
        /// <exception cref="InvalidOperationException">
        ///     The player is not in the valid state.\n
        ///     -or-\n
        ///     No subtitle is set.
        /// </exception>
        /// <seealso cref="SetSubtitle(string)"/>
        public void SetSubtitleOffset(int offset)
        {
            ValidatePlayerState(PlayerState.Playing, PlayerState.Paused);

            var err = NativePlayer.SetSubtitlePositionOffset(Handle, offset);

            if (err == PlayerErrorCode.FeatureNotSupported)
            {
                throw new InvalidOperationException("No subtitle set");
            }

            err.ThrowIfFailed("Failed to the subtitle offset of the player");
            Log.Debug(PlayerLog.Tag, PlayerLog.Leave);
        }

        private void Prepare()
        {
            NativePlayer.Prepare(Handle).ThrowIfFailed("Failed to prepare the player");
        }

        /// <summary>
        /// Called when the <see cref="Prepare"/> is invoked.
        /// </summary>
        protected virtual void OnPreparing()
        {
            RegisterEvents();
        }

        /// <summary>
        /// Prepares the media player for playback, asynchronously.
        /// </summary>
        /// <returns>A task that represents the asynchronous prepare operation.</returns>
        /// <remarks>To prepare the player, the player must be in the <see cref="PlayerState.Idle"/> state,
        ///     and a source must be set.</remarks>
        /// <exception cref="InvalidOperationException">No source is set.</exception>
        /// <exception cref="ObjectDisposedException">The player has already been disposed of.</exception>
        /// <exception cref="InvalidOperationException">The player is not in the valid state.</exception>
        public virtual Task PrepareAsync()
        {
            Log.Debug(PlayerLog.Tag, PlayerLog.Enter);
            if (_source == null)
            {
                throw new InvalidOperationException("No source is set.");
            }

            ValidatePlayerState(PlayerState.Idle);

            OnPreparing();

            var completionSource = new TaskCompletionSource<bool>();

            SetPreparing();

            Task.Run(() =>
            {
                try
                {
                    Prepare();
                    ClearPreparing();
                    completionSource.SetResult(true);
                }
                catch (Exception e)
                {
                    ClearPreparing();
                    completionSource.TrySetException(e);
                }
            });
            Log.Debug(PlayerLog.Tag, PlayerLog.Leave);

            return completionSource.Task;
        }

        /// <summary>
        /// Unprepares the player.
        /// </summary>
        /// <remarks>
        ///     The most recently used source is reset and no longer associated with the player. Playback is no longer possible.
        ///     If you want to use the player again, you have to set a source and call <see cref="PrepareAsync"/> again.
        ///     <para>
        ///     The player must be in the <see cref="PlayerState.Ready"/>, <see cref="PlayerState.Playing"/> or <see cref="PlayerState.Paused"/> state.
        ///     It has no effect if the player is already in the <see cref="PlayerState.Idle"/> state.
        ///     </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The player has already been disposed of.</exception>
        /// <exception cref="InvalidOperationException">The player is not in the valid state.</exception>
        public virtual void Unprepare()
        {
            Log.Debug(PlayerLog.Tag, PlayerLog.Enter);
            if (State == PlayerState.Idle)
            {
                Log.Warn(PlayerLog.Tag, "idle state already");
                return;
            }
            ValidatePlayerState(PlayerState.Ready, PlayerState.Paused, PlayerState.Playing);

            NativePlayer.Unprepare(Handle).ThrowIfFailed("Failed to unprepare the player");

            OnUnprepared();
        }

        /// <summary>
        /// Called after the <see cref="Player"/> is unprepared.
        /// </summary>
        /// <seealso cref="Unprepare"/>
        protected virtual void OnUnprepared()
        {
            _source?.DetachFrom(this);
            _source = null;
        }

        /// <summary>
        /// Starts or resumes playback.
        /// </summary>
        /// <remarks>
        /// The player must be in the <see cref="PlayerState.Ready"/> or <see cref="PlayerState.Paused"/> state.
        /// It has no effect if the player is already in the <see cref="PlayerState.Playing"/> state.\n
        /// \n
        /// Sound can be mixed with other sounds if you don't control the stream focus using <see cref="ApplyAudioStreamPolicy"/>.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The player has already been disposed of.</exception>
        /// <exception cref="InvalidOperationException">The player is not in the valid state.</exception>
        /// <seealso cref="PrepareAsync"/>
        /// <seealso cref="Stop"/>
        /// <seealso cref="Pause"/>
        /// <seealso cref="PlaybackCompleted"/>
        /// <seealso cref="ApplyAudioStreamPolicy"/>
        public virtual void Start()
        {
            Log.Debug(PlayerLog.Tag, PlayerLog.Enter);
            if (State == PlayerState.Playing)
            {
                Log.Warn(PlayerLog.Tag, "playing state already");
                return;
            }
            ValidatePlayerState(PlayerState.Ready, PlayerState.Paused);

            NativePlayer.Start(Handle).ThrowIfFailed("Failed to start the player");
            Log.Debug(PlayerLog.Tag, PlayerLog.Leave);
        }

        /// <summary>
        /// Stops playing media content.
        /// </summary>
        /// <remarks>
        /// The player must be in the <see cref="PlayerState.Playing"/> or <see cref="PlayerState.Paused"/> state.
        /// It has no effect if the player is already in the <see cref="PlayerState.Ready"/> state.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The player has already been disposed of.</exception>
        /// <exception cref="InvalidOperationException">The player is not in the valid state.</exception>
        /// <seealso cref="Start"/>
        /// <seealso cref="Pause"/>
        public virtual void Stop()
        {
            Log.Debug(PlayerLog.Tag, PlayerLog.Enter);
            if (State == PlayerState.Ready)
            {
                Log.Warn(PlayerLog.Tag, "ready state already");
                return;
            }
            ValidatePlayerState(PlayerState.Paused, PlayerState.Playing);

            NativePlayer.Stop(Handle).ThrowIfFailed("Failed to stop the player");
            Log.Debug(PlayerLog.Tag, PlayerLog.Leave);
        }

        /// <summary>
        /// Pauses the player.
        /// </summary>
        /// <remarks>
        /// The player must be in the <see cref="PlayerState.Playing"/> state.
        /// It has no effect if the player is already in the <see cref="PlayerState.Paused"/> state.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The player has already been disposed of.</exception>
        /// <exception cref="InvalidOperationException">The player is not in the valid state.</exception>
        /// <seealso cref="Start"/>
        public virtual void Pause()
        {
            Log.Debug(PlayerLog.Tag, PlayerLog.Enter);
            if (State == PlayerState.Paused)
            {
                Log.Warn(PlayerLog.Tag, "pause state already");
                return;
            }

            ValidatePlayerState(PlayerState.Playing);

            NativePlayer.Pause(Handle).ThrowIfFailed("Failed to pause the player");
            Log.Debug(PlayerLog.Tag, PlayerLog.Leave);
        }

        private MediaSource _source;

        /// <summary>
        /// Sets a media source for the player.
        /// </summary>
        /// <param name="source">A <see cref="MediaSource"/> that specifies the source for playback.</param>
        /// <remarks>The player must be in the <see cref="PlayerState.Idle"/> state.</remarks>
        /// <exception cref="ObjectDisposedException">The player has already been disposed of.</exception>
        /// <exception cref="InvalidOperationException">
        ///     The player is not in the valid state.\n
        ///     -or-\n
        ///     It is not able to assign the source to the player.
        ///     </exception>
        /// <seealso cref="PrepareAsync"/>
        public void SetSource(MediaSource source)
        {
            Log.Debug(PlayerLog.Tag, PlayerLog.Enter);
            ValidatePlayerState(PlayerState.Idle);

            if (source != null)
            {
                source.AttachTo(this);
            }

            if (_source != null)
            {
                _source.DetachFrom(this);
            }

            _source = source;
            Log.Debug(PlayerLog.Tag, PlayerLog.Leave);
        }

        /// <summary>
        /// Captures a video frame asynchronously.
        /// </summary>
        /// <returns>A task that represents the asynchronous capture operation.</returns>
        /// <feature>http://tizen.org/feature/multimedia.raw_video</feature>
        /// <remarks>The player must be in the <see cref="PlayerState.Playing"/> or <see cref="PlayerState.Paused"/> state.</remarks>
        /// <exception cref="ObjectDisposedException">The player has already been disposed of.</exception>
        /// <exception cref="InvalidOperationException">The player is not in the valid state.</exception>
        /// <exception cref="NotSupportedException">The required feature is not supported.</exception>
        public async Task<CapturedFrame> CaptureVideoAsync()
        {
            Log.Debug(PlayerLog.Tag, PlayerLog.Enter);

            ValidationUtil.ValidateFeatureSupported(Features.RawVideo);

            ValidatePlayerState(PlayerState.Playing, PlayerState.Paused);

            TaskCompletionSource<CapturedFrame> t = new TaskCompletionSource<CapturedFrame>();

            NativePlayer.VideoCaptureCallback cb = (data, width, height, size, _) =>
            {
                Debug.Assert(size <= int.MaxValue);

                byte[] buf = new byte[size];
                Marshal.Copy(data, buf, 0, (int)size);

                t.TrySetResult(new CapturedFrame(buf, width, height));
            };

            using (var cbKeeper = ObjectKeeper.Get(cb))
            {
                NativePlayer.CaptureVideo(Handle, cb)
                    .ThrowIfFailed("Failed to capture the video");

                return await t.Task;
            }
        }

        /// <summary>
        /// Gets the play position in milliseconds.
        /// </summary>
        /// <remarks>The player must be in the <see cref="PlayerState.Ready"/>, <see cref="PlayerState.Playing"/> or <see cref="PlayerState.Paused"/> state.</remarks>
        /// <exception cref="ObjectDisposedException">The player has already been disposed of.</exception>
        /// <exception cref="InvalidOperationException">The player is not in the valid state.</exception>
        /// <seealso cref="SetPlayPositionAsync(int, bool)"/>
        public int GetPlayPosition()
        {
            Log.Debug(PlayerLog.Tag, PlayerLog.Enter);
            ValidatePlayerState(PlayerState.Ready, PlayerState.Paused, PlayerState.Playing);

            int playPosition = 0;

            NativePlayer.GetPlayPosition(Handle, out playPosition).
                ThrowIfFailed("Failed to get the play position of the player");

            Log.Info(PlayerLog.Tag, "get play position : " + playPosition);

            return playPosition;
        }

        private void SetPlayPosition(int milliseconds, bool accurate,
            NativePlayer.SeekCompletedCallback cb)
        {
            Log.Debug(PlayerLog.Tag, PlayerLog.Enter);
            var ret = NativePlayer.SetPlayPosition(Handle, milliseconds, accurate, cb, IntPtr.Zero);

            //Note that we assume invalid param error is returned only when the position value is invalid.
            if (ret == PlayerErrorCode.InvalidArgument)
            {
                throw new ArgumentOutOfRangeException(nameof(milliseconds), milliseconds,
                    "The position is not valid.");
            }
            if (ret != PlayerErrorCode.None)
            {
                Log.Error(PlayerLog.Tag, "Failed to set play position, " + (PlayerError)ret);
            }
            ret.ThrowIfFailed("Failed to set play position");
        }

        /// <summary>
        /// Sets the seek position for playback, asynchronously.
        /// </summary>
        /// <param name="position">The value indicating a desired position in milliseconds.</param>
        /// <param name="accurate">The value indicating whether the operation performs with accuracy.</param>
        /// <remarks>
        ///     <para>The player must be in the <see cref="PlayerState.Ready"/>, <see cref="PlayerState.Playing"/> or <see cref="PlayerState.Paused"/> state.</para>
        ///     <para>If the <paramref name="accurate"/> is true, the play position will be adjusted as the specified <paramref name="position"/> value,
        ///     but this might be considerably slow. If false, the play position will be a nearest keyframe position.</para>
        ///     </remarks>
        /// <exception cref="ObjectDisposedException">The player has already been disposed of.</exception>
        /// <exception cref="InvalidOperationException">The player is not in the valid state.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The specified position is not valid.</exception>
        /// <seealso cref="GetPlayPosition"/>
        public async Task SetPlayPositionAsync(int position, bool accurate)
        {
            Log.Debug(PlayerLog.Tag, PlayerLog.Enter);
            ValidatePlayerState(PlayerState.Ready, PlayerState.Playing, PlayerState.Paused);

            var taskCompletionSource = new TaskCompletionSource<bool>();

            bool immediateResult = _source is MediaStreamSource;

            NativePlayer.SeekCompletedCallback cb = _ => taskCompletionSource.TrySetResult(true);

            using (var cbKeeper = ObjectKeeper.Get(cb))
            {
                SetPlayPosition(position, accurate, cb);
                if (immediateResult)
                {
                    taskCompletionSource.TrySetResult(true);
                }

                await taskCompletionSource.Task;
            }

            Log.Debug(PlayerLog.Tag, PlayerLog.Leave);
        }

        /// <summary>
        /// Sets playback rate.
        /// </summary>
        /// <param name="rate">The value for the playback rate. Valid range is -5.0 to 5.0, inclusive.</param>
        /// <remarks>
        ///     <para>The player must be in the <see cref="PlayerState.Ready"/>, <see cref="PlayerState.Playing"/> or <see cref="PlayerState.Paused"/> state.</para>
        ///     <para>The sound will be muted, when the playback rate is under 0.0 or over 2.0.</para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The player has already been disposed of.</exception>
        /// <exception cref="InvalidOperationException">
        ///     The player is not in the valid state.\n
        ///     -or-\n
        ///     Streaming playback.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     <paramref name="rate"/> is less than 5.0.\n
        ///     -or-\n
        ///     <paramref name="rate"/> is greater than 5.0.\n
        ///     -or-\n
        ///     <paramref name="rate"/> is zero.
        /// </exception>
        public void SetPlaybackRate(float rate)
        {
            Log.Debug(PlayerLog.Tag, PlayerLog.Enter);
            if (rate < -5.0F || 5.0F < rate || rate == 0.0F)
            {
                throw new ArgumentOutOfRangeException(nameof(rate), rate, "Valid range is -5.0 to 5.0 (except 0.0)");
            }

            ValidatePlayerState(PlayerState.Ready, PlayerState.Playing, PlayerState.Paused);

            NativePlayer.SetPlaybackRate(Handle, rate).ThrowIfFailed("Failed to set the playback rate.");
            Log.Debug(PlayerLog.Tag, PlayerLog.Leave);
        }

        /// <summary>
        /// Applies the audio stream policy.
        /// </summary>
        /// <param name="policy">The <see cref="AudioStreamPolicy"/> to apply.</param>
        /// <remarks>
        /// The player must be in the <see cref="PlayerState.Idle"/> state.\n
        /// \n
        /// <see cref="Player"/> does not support all <see cref="AudioStreamType"/>.\n
        /// Supported types are <see cref="AudioStreamType.Media"/>, <see cref="AudioStreamType.System"/>,
        /// <see cref="AudioStreamType.Alarm"/>, <see cref="AudioStreamType.Notification"/>,
        /// <see cref="AudioStreamType.Emergency"/>, <see cref="AudioStreamType.VoiceInformation"/>,
        /// <see cref="AudioStreamType.RingtoneVoip"/> and <see cref="AudioStreamType.MediaExternalOnly"/>.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        ///     The player has already been disposed of.\n
        ///     -or-\n
        ///     <paramref name="policy"/> has already been disposed of.
        /// </exception>
        /// <exception cref="InvalidOperationException">The player is not in the valid state.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
        /// <exception cref="NotSupportedException">
        ///     <see cref="AudioStreamType"/> of <paramref name="policy"/> is not supported by <see cref="Player"/>.
        /// </exception>
        /// <seealso cref="AudioStreamPolicy"/>
        public void ApplyAudioStreamPolicy(AudioStreamPolicy policy)
        {
            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            ValidatePlayerState(PlayerState.Idle);

            NativePlayer.SetAudioPolicyInfo(Handle, policy.Handle).
                ThrowIfFailed("Failed to set the audio stream policy to the player");
        }
        #endregion

        #region Preparing state

        private int _isPreparing;

        private bool IsPreparing()
        {
            return Interlocked.CompareExchange(ref _isPreparing, 1, 1) == 1;
        }

        private void SetPreparing()
        {
            Interlocked.Exchange(ref _isPreparing, 1);
        }

        private void ClearPreparing()
        {
            Interlocked.Exchange(ref _isPreparing, 0);
        }

        #endregion

        /// <summary>
        /// This method supports the product infrastructure and is not intended to be used directly from application code.
        /// </summary>
        protected static Exception GetException(int errorCode, string message) =>
            ((PlayerErrorCode)errorCode).GetException(message);
    }
}