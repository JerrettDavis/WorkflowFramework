window.voiceRecorder = (() => {
  let mediaStream = null;
  let mediaRecorder = null;
  let chunks = [];
  let activePreviewUrl = null;
  let recognition = null;
  let dotNetRef = null;
  let finalTranscript = "";
  let interimTranscript = "";

  const SpeechRecognitionCtor = window.SpeechRecognition || window.webkitSpeechRecognition;

  async function listInputDevices() {
    if (!navigator.mediaDevices || !navigator.mediaDevices.enumerateDevices) {
      return [];
    }

    let devices = [];
    try {
      devices = await navigator.mediaDevices.enumerateDevices();
    } catch {
      return [];
    }

    let inputs = devices.filter((d) => d.kind === "audioinput");

    // If labels are unavailable, try permission once but don't fail recording setup if denied.
    if (inputs.length > 0 && inputs.every((d) => !d.label)) {
      try {
        await ensureMicPermission();
        const refreshed = await navigator.mediaDevices.enumerateDevices();
        inputs = refreshed.filter((d) => d.kind === "audioinput");
      } catch {
        // Best effort only; fallback to generic labels below.
      }
    }

    return inputs.map((d, i) => ({
      deviceId: d.deviceId,
      label: d.label || `Microphone ${i + 1}`,
    }));
  }

  async function startRecording(deviceId, dotNetObjectRef) {
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia || typeof MediaRecorder === "undefined") {
      throw new Error("Recording is not supported by this browser.");
    }

    if (mediaRecorder && mediaRecorder.state !== "inactive") {
      throw new Error("Recording is already in progress.");
    }

    dotNetRef = dotNetObjectRef || null;
    finalTranscript = "";
    interimTranscript = "";
    notifyTranscript();

    const constraints = deviceId
      ? { audio: { deviceId: { exact: deviceId } } }
      : { audio: true };

    try {
      mediaStream = await navigator.mediaDevices.getUserMedia(constraints);
      chunks = [];

      const preferred = "audio/webm;codecs=opus";
      const options = MediaRecorder.isTypeSupported(preferred) ? { mimeType: preferred } : undefined;
      mediaRecorder = options ? new MediaRecorder(mediaStream, options) : new MediaRecorder(mediaStream);

      mediaRecorder.ondataavailable = (event) => {
        if (event.data && event.data.size > 0) {
          chunks.push(event.data);
        }
      };

      mediaRecorder.start(250);
      startLiveTranscription();

      return {
        isRecording: true,
        speechRecognition: !!SpeechRecognitionCtor,
      };
    } catch (error) {
      stopLiveTranscription();
      cleanupRecorder();
      throw error;
    }
  }

  async function stopRecording() {
    stopLiveTranscription();

    if (!mediaRecorder) {
      cleanupRecorder();
      return null;
    }

    if (mediaRecorder.state === "inactive") {
      if (chunks.length === 0) {
        cleanupRecorder();
        return null;
      }
      return await payloadFromChunks(mediaRecorder.mimeType);
    }

    return await new Promise((resolve, reject) => {
      const recorder = mediaRecorder;
      let settled = false;
      const timeout = setTimeout(() => {
        if (settled) return;
        settled = true;
        cleanupRecorder();
        resolve(null);
      }, 6000);

      recorder.onerror = (event) => {
        if (settled) return;
        settled = true;
        clearTimeout(timeout);
        cleanupRecorder();
        reject(event.error || new Error("Recorder error."));
      };

      recorder.onstop = async () => {
        if (settled) return;
        settled = true;
        clearTimeout(timeout);

        try {
          const payload = await payloadFromChunks(recorder.mimeType);
          resolve(payload);
        } catch (error) {
          reject(error);
        }
      };

      try {
        recorder.stop();
      } catch (error) {
        clearTimeout(timeout);
        cleanupRecorder();
        reject(error);
      }
    });
  }

  function isRecording() {
    return !!mediaRecorder && mediaRecorder.state !== "inactive";
  }

  function supportsSpeechRecognition() {
    return !!SpeechRecognitionCtor;
  }

  function clearPreview() {
    if (activePreviewUrl) {
      URL.revokeObjectURL(activePreviewUrl);
      activePreviewUrl = null;
    }
  }

  function dispose() {
    stopLiveTranscription();
    cleanupRecorder();
    clearPreview();
    finalTranscript = "";
    interimTranscript = "";
    dotNetRef = null;
  }

  async function ensureMicPermission() {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    stream.getTracks().forEach((track) => track.stop());
  }

  function startLiveTranscription() {
    if (!SpeechRecognitionCtor) {
      return;
    }

    stopLiveTranscription();
    recognition = new SpeechRecognitionCtor();
    recognition.lang = "en-US";
    recognition.interimResults = true;
    recognition.continuous = true;

    recognition.onresult = (event) => {
      let latestFinal = "";
      let latestInterim = "";

      for (let i = event.resultIndex; i < event.results.length; i++) {
        const transcript = event.results[i][0] ? event.results[i][0].transcript || "" : "";
        if (!transcript) continue;
        if (event.results[i].isFinal) {
          latestFinal += transcript.trim() + " ";
        } else {
          latestInterim += transcript;
        }
      }

      if (latestFinal) {
        finalTranscript = `${finalTranscript} ${latestFinal}`.trim();
      }
      interimTranscript = latestInterim.trim();
      notifyTranscript();
    };

    recognition.onerror = () => {
      interimTranscript = "";
      notifyTranscript();
    };

    recognition.onend = () => {
      if (isRecording()) {
        try {
          recognition.start();
        } catch {
          // Browser throttled restarts.
        }
      }
    };

    try {
      recognition.start();
    } catch {
      recognition = null;
    }
  }

  function stopLiveTranscription() {
    if (!recognition) {
      return;
    }

    const active = recognition;
    recognition = null;
    try {
      active.onresult = null;
      active.onerror = null;
      active.onend = null;
      active.stop();
    } catch {
      // Ignore best-effort stop errors.
    }
  }

  async function payloadFromChunks(mimeType) {
    const sourceBlob = new Blob(chunks, { type: mimeType || "audio/webm" });
    const fileName = fileNameForMime(sourceBlob.type || mimeType || "audio/webm");
    const base64 = await blobToBase64(sourceBlob);

    if (activePreviewUrl) {
      URL.revokeObjectURL(activePreviewUrl);
    }
    activePreviewUrl = URL.createObjectURL(sourceBlob);

    const payload = {
      fileName,
      mimeType: sourceBlob.type || "audio/webm",
      size: sourceBlob.size,
      base64,
      previewUrl: activePreviewUrl,
      liveTranscript: finalTranscript.trim(),
    };

    cleanupRecorder();
    return payload;
  }

  function cleanupRecorder() {
    if (mediaStream) {
      mediaStream.getTracks().forEach((track) => track.stop());
      mediaStream = null;
    }
    mediaRecorder = null;
    chunks = [];
  }

  function fileNameForMime(mimeType) {
    const type = `${mimeType || ""}`.toLowerCase();
    if (type.includes("wav")) return "recording.wav";
    if (type.includes("mp4")) return "recording.m4a";
    if (type.includes("ogg")) return "recording.ogg";
    if (type.includes("mpeg") || type.includes("mp3")) return "recording.mp3";
    return "recording.webm";
  }

  function blobToBase64(blob) {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onloadend = () => {
        const result = reader.result;
        if (typeof result !== "string") {
          reject(new Error("Unable to read audio blob."));
          return;
        }
        const comma = result.indexOf(",");
        resolve(comma >= 0 ? result.substring(comma + 1) : result);
      };
      reader.onerror = () => reject(reader.error || new Error("Unable to read audio blob."));
      reader.readAsDataURL(blob);
    });
  }

  function notifyTranscript() {
    if (!dotNetRef) return;
    dotNetRef.invokeMethodAsync("OnLiveTranscriptUpdated", finalTranscript, interimTranscript).catch(() => {});
  }

  return {
    listInputDevices,
    startRecording,
    stopRecording,
    isRecording,
    supportsSpeechRecognition,
    clearPreview,
    dispose,
  };
})();
