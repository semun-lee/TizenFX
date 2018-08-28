/*
* Copyright (c) 2018 Samsung Electronics Co., Ltd All Rights Reserved
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
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Tizen.Applications;
using Tizen.Applications.RPCPort;


namespace Tizen.Nlp
{

    /// <summary>
    /// This class contains the methods in the NLP processing.
    /// </summary>
    /// <since_tizen> 5 </since_tizen>
    public class NaturalLanguageProcess
    {
        private Message _msg;
        private readonly Message.NotifyCb _noti = new Message.NotifyCb();
        private readonly string _tag;
        private const string ServiceId = "org.tizen.nlp.service";

        private delegate bool LangDetectCallback(MessageReceivedEventArgs e);

        private bool _isConnected = false;

        private delegate bool WordTokenizeCallback(MessageReceivedEventArgs e);

        private delegate bool PostagCallback(MessageReceivedEventArgs e);

        private delegate bool NamedEntityRecognitionCallback(MessageReceivedEventArgs e);

        private delegate bool LemmatizeCallback(MessageReceivedEventArgs e);

        private int _requestIdPos = 0;
        private int _requestIdLang = 0;
        private int _requestIdNeChunk = 0;
        private int _requestIdWordTokenize = 0;
        private int _requestIdLemmatize = 0;
        private readonly Dictionary<int, PostagCallback> _mapsPosTag = new Dictionary<int, PostagCallback>();

        /// <summary>
        /// An delegate method about service disconnected .
        /// </summary>
        /// <since_tizen> 5 </since_tizen>
        public delegate bool DisconnectedCallback(EventArgs e);

        /// <summary>
        /// An delegate handle to expose to user
        /// </summary>
        /// <since_tizen> 5 </since_tizen>
        public DisconnectedCallback DisconnectEvent { get; set; }

        private readonly Dictionary<int, LangDetectCallback>
            _mapsLangDetect = new Dictionary<int, LangDetectCallback>();

        private readonly Dictionary<int, WordTokenizeCallback> _mapsWordTokenize =
            new Dictionary<int, WordTokenizeCallback>();

        private readonly Dictionary<int, NamedEntityRecognitionCallback> _mapsNamedEntity =
            new Dictionary<int, NamedEntityRecognitionCallback>();

        private readonly Dictionary<int, LemmatizeCallback> _mapsLemmatize = new Dictionary<int, LemmatizeCallback>();

        private void MakeRequest(string cmd, string sentence, int requestid)
        {
            if (_isConnected)
            {
                Bundle b = new Bundle();
                b.AddItem("command", cmd);
                b.AddItem("info", sentence);
                b.AddItem("request_id", requestid.ToString());
                _msg.Send(b);
            }
            else
            {
                throw new NotConnectedSocketException();
            }
        }

        private void ResultReceived(string sender, Bundle msg)
        {
            Log.Debug(_tag, "OnReceived ++");
            MessageReceivedEventArgs e = new MessageReceivedEventArgs();
            int requestid;
            Dictionary<string, string[]> result = new Dictionary<string, string[]>();
            if (msg.GetItem("command").Equals("word_tokenize"))
            {
                result.Add("token", (string[])msg.GetItem("return_token"));
                e.RequestId = int.Parse((string)msg.GetItem("request_id"));
                requestid = int.Parse((string)msg.GetItem("request_id"));
                e.RequestId = requestid;
                e.Message = result;
                if (_mapsWordTokenize.ContainsKey(requestid))
                {
                    _mapsWordTokenize[requestid]?.Invoke(e);
                    _mapsWordTokenize.Remove(requestid);
                }
            }
            else if (msg.GetItem("command").Equals("pos_tag"))
            {
                result.Add("token", (string[])msg.GetItem("return_token"));
                result.Add("tag", (string[])msg.GetItem("return_tag"));
                requestid = int.Parse((string)msg.GetItem("request_id"));
                e.RequestId = requestid;
                e.Message = result;
                if (_mapsPosTag.ContainsKey(requestid))
                {
                    _mapsPosTag[requestid]?.Invoke(e);
                    _mapsPosTag.Remove(requestid);
                }
            }
            else if (msg.GetItem("command").Equals("ne_chunk"))
            {
                result.Add("token", (string[])msg.GetItem("return_token"));
                result.Add("tag", (string[])msg.GetItem("return_tag"));
                requestid = int.Parse((string)msg.GetItem("request_id"));
                e.RequestId = requestid;
                e.Message = result;
                if (_mapsNamedEntity.ContainsKey(requestid))
                {
                    _mapsNamedEntity[requestid]?.Invoke(e);
                    _mapsNamedEntity.Remove(requestid);
                }
            }
            else if (msg.GetItem("command").Equals("lemmatize"))
            {
                result.Add("token", (string[])msg.GetItem("return_token"));
                requestid = int.Parse((string)msg.GetItem("request_id"));
                e.RequestId = requestid;
                e.Message = result;
                if (_mapsLemmatize.ContainsKey(requestid))
                {
                    _mapsLemmatize[requestid]?.Invoke(e);
                    _mapsLemmatize.Remove(requestid);
                }
            }
            else if (msg.GetItem("command").Equals("langdetect"))
            {
                result.Add("token", (string[])msg.GetItem("return_token"));
                requestid = int.Parse((string)msg.GetItem("request_id"));
                e.RequestId = requestid;
                e.Message = result;
                if (_mapsLangDetect.ContainsKey(requestid))
                {
                    _mapsLangDetect[requestid]?.Invoke(e);
                    _mapsLangDetect.Remove(requestid);
                }
            }
            else
            {
                return;
            }

            Log.Debug(_tag, "done");
        }

        /// <summary>
        /// An construct method  to init local env of NLP .
        /// </summary>
        /// <since_tizen> 5 </since_tizen>
        public NaturalLanguageProcess()
        {
            _tag = Application.Current.ApplicationInfo.ApplicationId;
            Log.Debug(_tag, "msg construct started");
            _msg = new Message(ServiceId);
            _msg.Disconnected += (s, e) =>
            {
                _isConnected = false;
                DisconnectEvent?.Invoke(e);
            };
            Log.Debug(_tag, "msg construct success");
            _noti.Received += ResultReceived;
            Log.Debug(_tag, "notify callback be assigned");
            Log.Debug(_tag, "start to connect");
            Log.Debug(_tag, "wait to callback of onConnected");
        }


        /// <summary>
        /// An async method  to connect remote service.
        /// </summary>
        /// <since_tizen> 5 </since_tizen>
        /// <exception cref="InvalidIDException">Thrown when the connect is rejected.</exception>
        public Task Connect()
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            _msg.Connected += (sender, e) =>
            {
                Log.Debug(_tag, "start to register");
                _msg.CoRegister(Application.Current.ApplicationInfo.ApplicationId, _noti);
                Log.Debug(_tag, "connected callback be called");
                tcs.SetResult(true);
                _isConnected = true;
            };
            _msg.Rejected += (sender, e) => { tcs.SetException(new InvalidIDException()); };
            _msg.Connect();
            return tcs.Task;
        }

        private void Close()
        {
            _noti.Received -= ResultReceived;
            _msg.UnRegister();
            _msg.Dispose();
            _isConnected = false;
            _msg = null;
        }


        private void MapClear()
        {
            foreach (var key in _mapsWordTokenize.Keys.ToList())
            {
                _mapsWordTokenize[key] = null;
            }

            foreach (var key in _mapsPosTag.Keys.ToList())
            {
                _mapsPosTag[key] = null;
            }

            foreach (var key in _mapsNamedEntity.Keys.ToList())
            {
                _mapsNamedEntity[key] = null;
            }

            foreach (var key in _mapsLemmatize.Keys.ToList())
            {
                _mapsLemmatize[key] = null;
            }

            foreach (var key in _mapsLangDetect.Keys.ToList())
            {
                _mapsLangDetect[key] = null;
            }
        }

        /// <summary>
        /// A method to release resource of library
        /// </summary>
        /// <since_tizen> 5 </since_tizen>
        public void Dispose()
        {
            Close();
            MapClear();
        }

        /// <summary>
        /// Send Pos of Tag request to remote tidl service with one parameters.
        /// </summary>
        /// <param name="sentence">A sentence need to be processed.</param>
        /// <returns>PosTagResult</returns>
        /// <since_tizen> 5 </since_tizen>
        public Task<PosTagResult> PosTagAsync(string sentence)
        {
            int id = _requestIdPos++;
            MakeRequest("pos_tag", sentence, id);
            var task = new TaskCompletionSource<PosTagResult>();
            _mapsPosTag[id] = (e) =>
            {
                PosTagResult pr = new PosTagResult();
                e.Message.TryGetValue("token", out string[] tokens);
                e.Message.TryGetValue("tag", out string[] tags);
                pr.Tokens = tokens;
                pr.Tags = tags;
                task.SetResult(pr);
                return true;
            };
            return task.Task;
        }

        /// <summary>
        /// Send Named Entity recognition request to remote tidl service with one parameters.
        /// </summary>
        /// <param name="sentence">A sentence need to be processed.</param>
        /// <returns>NamedEntityRecognitionResult</returns>
        /// <since_tizen> 5 </since_tizen>
        public Task<NamedEntityRecognitionResult> NamedEntityRecognitionAsync(string sentence)
        {
            int id = _requestIdNeChunk++;
            MakeRequest("ne_chunk", sentence, id);
            var task = new TaskCompletionSource<NamedEntityRecognitionResult>();
            _mapsNamedEntity[id] = (e) =>
            {
                NamedEntityRecognitionResult nr = new NamedEntityRecognitionResult();
                e.Message.TryGetValue("token", out string[] tokens);
                e.Message.TryGetValue("tag", out string[] tags);
                nr.Tokens = tokens;
                nr.Tags = tags;
                task.SetResult(nr);
                return true;
            };
            return task.Task;
        }

        /// <summary>
        /// Send language detect request to remote tidl service with one parameters.
        /// </summary>
        /// <param name="sentence">A sentence need to be processed.</param>
        /// <returns>LanguageDetectedResult</returns>
        /// <since_tizen> 5 </since_tizen>
        public Task<LanguageDetectedResult> LanguageDetectAsync(string sentence)
        {
            int id = _requestIdLang++;
            MakeRequest("langdetect", sentence, id);
            var task = new TaskCompletionSource<LanguageDetectedResult>();
            _mapsLangDetect[id] = (e) =>
            {
                LanguageDetectedResult lr = new LanguageDetectedResult();
                e.Message.TryGetValue("token", out string[] lang);
                if (lang != null) lr.Language = lang[0];
                task.SetResult(lr);
                return true;
            };
            return task.Task;
        }

        /// <summary>
        /// Send Lemmatize request to remote tidl service with one parameters.
        /// </summary>
        /// <param name="sentence">A sentence need to be processed.</param>
        /// <returns>ProcessResult</returns>
        /// <since_tizen> 5 </since_tizen>
        public Task<LemmatizeResult> LemmatizeaAsync(string sentence)
        {
            int id = _requestIdLemmatize++;
            MakeRequest("lemmatize", sentence, id);
            var task = new TaskCompletionSource<LemmatizeResult>();
            _mapsLemmatize[id] = (e) =>
            {
                LemmatizeResult mr = new LemmatizeResult();
                e.Message.TryGetValue("token", out string[] tokens);
                if (tokens != null) mr.ActualWords = tokens[0];
                task.SetResult(mr);
                return true;
            };
            return task.Task;
        }

        /// <summary>
        /// Send word tokenize request to remote tidl service with one parameters.
        /// </summary>
        /// <param name="sentence">A sentence need to be processed.</param>
        /// <returns>ProcessResult</returns>
        /// <since_tizen> 5 </since_tizen>
        public Task<WordTokenizeResult> WordTokenizeAsync(string sentence)
        {
            int id = _requestIdWordTokenize++;
            MakeRequest("word_tokenize", sentence, id);
            var task = new TaskCompletionSource<WordTokenizeResult>();
            _mapsWordTokenize[id] = (e) =>
            {
                WordTokenizeResult wr = new WordTokenizeResult();
                e.Message.TryGetValue("token", out string[] tokens);
                wr.Tokens = tokens;
                task.SetResult(wr);
                return true;
            };
            return task.Task;
        }
    }
}