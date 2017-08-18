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
using System.Diagnostics;

namespace Tizen.Multimedia
{
    /// <summary>
    /// Provides the ability to control a sound stream.
    /// </summary>
    public class AudioStreamPolicy : IDisposable
    {
        private AudioStreamPolicyHandle _handle;
        private bool _disposed = false;
        private Interop.AudioStreamPolicy.FocusStateChangedCallback _focusStateChangedCallback;

        /// <summary>
        ///  Initializes a new instance of the <see cref="AudioStreamPolicy"/> class with <see cref="AudioStreamType"/>
        /// </summary>
        /// <remarks>
        /// To apply the stream policy according to this stream information, the AudioStreamPolicy should
        /// be passed to other APIs related to playback or recording. (e.g., <see cref="Player"/>, <see cref="WavPlayer"/> , etc.)
        /// </remarks>
        /// <param name="streamType">Type of sound stream for which policy needs to be created.</param>
        public AudioStreamPolicy(AudioStreamType streamType)
        {
            ValidationUtil.ValidateEnum(typeof(AudioStreamType), streamType, nameof(streamType));

            _focusStateChangedCallback = (IntPtr streamInfo, AudioStreamFocusOptions focusMask,
                AudioStreamFocusState state, AudioStreamFocusChangedReason reason, AudioStreamBehaviors behaviors,
                string extraInfo, IntPtr userData) =>
            {
                FocusStateChanged?.Invoke(this,
                    new AudioStreamPolicyFocusStateChangedEventArgs(focusMask, state, reason, behaviors, extraInfo));
            };

            Interop.AudioStreamPolicy.Create(streamType, _focusStateChangedCallback,
                IntPtr.Zero, out _handle).Validate("Unable to create stream information");

            Debug.Assert(_handle != null);
        }

        /// <summary>
        /// Occurs when the state of focus that belongs to the current AudioStreamPolicy is changed.
        /// </summary>
        /// <remarks>
        /// The event is raised in the internal thread.
        /// </remarks>
        public event EventHandler<AudioStreamPolicyFocusStateChangedEventArgs> FocusStateChanged;

        /// <summary>
        /// Gets the <see cref="AudioVolumeType"/>.
        /// </summary>
        /// <remarks>
        /// If the <see cref="AudioStreamType"/> of the current AudioStreamPolicy is <see cref="AudioStreamType.Emergency"/>,
        /// it returns <see cref="AudioVolumeType.None"/>.
        /// </remarks>
        /// <value>The <see cref="AudioVolumeType"/> of the policy instance.</value>
        public AudioVolumeType VolumeType
        {
            get
            {
                AudioVolumeType type;
                var ret = Interop.AudioStreamPolicy.GetSoundType(Handle, out type);
                if (ret == AudioManagerError.NoData)
                {
                    return AudioVolumeType.None;
                }

                ret.Validate("Failed to get volume type");

                return type;
            }
        }

        private AudioStreamFocusState GetFocusState(bool playback)
        {
            int ret = Interop.AudioStreamPolicy.GetFocusState(Handle, out var stateForPlayback, out var stateForRecording);
            MultimediaDebug.AssertNoError(ret);

            return playback ? stateForPlayback : stateForRecording;
        }

        /// <summary>
        /// Gets the state of focus for playback.
        /// </summary>
        /// <value>The state of focus for playback.</value>
        public AudioStreamFocusState PlaybackFocusState => GetFocusState(true);

        /// <summary>
        /// Gets the state of focus for recording.
        /// </summary>
        /// <value>The state of focus for recording.</value>
        public AudioStreamFocusState RecordingFocusState => GetFocusState(false);

        /// <summary>
        /// Gets or sets the auto focus reacquisition.
        /// </summary>
        /// <value>
        /// true if the auto focus reacquisition is enabled; otherwise, false.\n
        /// The default is true.
        /// </value>
        /// <remarks>
        /// If you don't want to reacquire the focus you've lost automatically,
        /// disable the focus reacquisition.
        /// </remarks>
        public bool FocusReacquisitionEnabled
        {
            get
            {
                Interop.AudioStreamPolicy.GetFocusReacquisition(Handle, out var enabled).
                    Validate("Failed to get focus reacquisition state");

                return enabled;
            }
            set
            {
                Interop.AudioStreamPolicy.SetFocusReacquisition(Handle, value).
                    Validate("Failed to set focus reacquisition");
            }
        }

        internal AudioStreamPolicyHandle Handle
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(AudioStreamPolicy));
                }
                return _handle;
            }
        }

        /// <summary>
        /// Acquires the stream focus.
        /// </summary>
        /// <param name="options">The focuses that you want to acquire.</param>
        /// <param name="behaviors">The requesting behaviors.</param>
        /// <param name="extraInfo">The extra information for this request. This value can be null.</param>
        public void AcquireFocus(AudioStreamFocusOptions options, AudioStreamBehaviors behaviors, string extraInfo)
        {
            if (options == 0)
            {
                throw new ArgumentException("options can't be zero.", nameof(options));
            }

            if (options.IsValid() == false)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options, "options contains a invalid bit.");
            }

            if (behaviors.IsValid() == false)
            {
                throw new ArgumentOutOfRangeException(nameof(behaviors), behaviors, "behaviors contains a invalid bit.");
            }

            Interop.AudioStreamPolicy.AcquireFocus(Handle, options, behaviors, extraInfo).
                Validate("Failed to acquire focus");
        }

        /// <summary>
        /// Releases the acquired focus.
        /// </summary>
        /// <param name="options">The focus mask that you want to release.</param>
        /// <param name="behaviors">The requesting behaviors.</param>
        /// <param name="extraInfo">The extra information for this request. This value can be null.</param>
        public void ReleaseFocus(AudioStreamFocusOptions options, AudioStreamBehaviors behaviors, string extraInfo)
        {
            if (options == 0)
            {
                throw new ArgumentException("options can't be zero.", nameof(options));
            }

            if (options.IsValid() == false)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options, "options contains a invalid bit.");
            }

            if (behaviors.IsValid() == false)
            {
                throw new ArgumentOutOfRangeException(nameof(behaviors), behaviors, "behaviors contains a invalid bit.");
            }

            Interop.AudioStreamPolicy.ReleaseFocus(Handle, options, behaviors, extraInfo).
                Validate("Failed to release focus");
        }

        /// <summary>
        /// Applies the stream routing.
        /// </summary>
        /// <remarks>
        /// If the stream has not been made yet, this will be applied when the stream starts to play.
        /// </remarks>
        /// <seealso cref="AddDeviceForStreamRouting(AudioDevice)"/>
        /// <seealso cref="RemoveDeviceForStreamRouting(AudioDevice)"/>
        public void ApplyStreamRouting()
        {
            Interop.AudioStreamPolicy.ApplyStreamRouting(Handle).Validate("Failed to apply stream routing");
        }

        /// <summary>
        /// Adds a device for the stream routing.
        /// </summary>
        /// <param name="device">The device to add.</param>
        /// <remarks>
        /// The available <see cref="AudioStreamType"/> is <see cref="AudioStreamType.Voip"/> and <see cref="AudioStreamType.MediaExternalOnly"/>.
        /// </remarks>
        /// <seealso cref="AudioManager.GetConnectedDevices()"/>
        /// <seealso cref="ApplyStreamRouting"/>
        public void AddDeviceForStreamRouting(AudioDevice device)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }
            var ret = Interop.AudioStreamPolicy.AddDeviceForStreamRouting(Handle, device.Id);

            if (ret == AudioManagerError.NoData)
            {
                throw new ArgumentException("The device seems not connected.", nameof(device));
            }

            ret.Validate("Failed to add device for stream routing");
        }

        /// <summary>
        /// Removes the device for the stream routing.
        /// </summary>
        /// <param name="device">The device to remove.</param>
        /// <remarks>
        /// The available <see cref="AudioStreamType"/> is <see cref="AudioStreamType.Voip"/> and <see cref="AudioStreamType.MediaExternalOnly"/>.
        /// </remarks>
        public void RemoveDeviceForStreamRouting(AudioDevice device)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }

            Interop.AudioStreamPolicy.RemoveDeviceForStreamRouting(Handle, device.Id).
                Validate("Failed to remove device for stream routing");
        }

        /// <summary>
        /// Releases all resources used by the <see cref="AudioStreamPolicy"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="AudioStreamPolicy"/>.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_handle != null)
                {
                    _handle.Dispose();
                }
                _disposed = true;
            }
        }

        #region Static events

        private static bool _isWatchCallbackRegistered;
        private static EventHandler<StreamFocusStateChangedEventArgs> _streamFocusStateChanged;
        private static Interop.AudioStreamPolicy.FocusStateWatchCallback _focusStateWatchCallback;
        private static object _streamFocusEventLock = new object();

        /// <summary>
        /// Occurs when the focus state for stream types is changed regardless of the process.
        /// </summary>
        public static event EventHandler<StreamFocusStateChangedEventArgs> StreamFocusStateChanged
        {
            add
            {
                lock (_streamFocusEventLock)
                {
                    if (_isWatchCallbackRegistered == false)
                    {
                        RegisterFocusStateWatch();
                        _isWatchCallbackRegistered = true;
                    }
                    _streamFocusStateChanged += value;
                }
            }
            remove
            {
                lock (_streamFocusEventLock)
                {
                    _streamFocusStateChanged -= value;
                }
            }
        }

        private static void RegisterFocusStateWatch()
        {
            _focusStateWatchCallback = (int id, AudioStreamFocusOptions options, AudioStreamFocusState focusState,
                AudioStreamFocusChangedReason reason, string extraInfo, IntPtr userData) =>
            {
                _streamFocusStateChanged?.Invoke(null,
                    new StreamFocusStateChangedEventArgs(options, focusState, reason, extraInfo));
            };

            Interop.AudioStreamPolicy.AddFocusStateWatchCallback(
                AudioStreamFocusOptions.Playback | AudioStreamFocusOptions.Recording,
                _focusStateWatchCallback, IntPtr.Zero, out var cbId).
                Validate("Failed to initialize focus state event");
        }
        #endregion
    }
}