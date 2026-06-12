mergeInto(LibraryManager.library, {
    // 实现C#中声明的所有函数
    StartMicrophoneRecording: function() {
        // 调用浏览器麦克风API开始录音（参考Web Audio API）
        navigator.mediaDevices.getUserMedia({ audio: true })
            .then(stream => {
                window.mediaRecorder = new MediaRecorder(stream);
                window.mediaRecorder.start();
                console.log("开始录音");
            })
            .catch(err => console.error("麦克风访问失败: ", err));
    },
    
    StopMicrophoneRecording: function() {
        if (window.mediaRecorder) {
            window.mediaRecorder.stop();
            console.log("停止录音");
            // 可在这里处理录音数据并传递给Unity
        }
    },
    
    WebGLIsRecording: function() {
        // 返回是否正在录音（1为正在录音，0为未录音）
        return window.mediaRecorder && window.mediaRecorder.state === "recording" ? 1 : 0;
    },
    
    WebGLStartMicrophone: function(frequency) {
        // 实现WebGL麦克风启动逻辑（与StartMicrophoneRecording类似，可复用代码）
        navigator.mediaDevices.getUserMedia({ audio: true })
            .then(stream => {
                window.webglMediaRecorder = new MediaRecorder(stream);
                window.webglMediaRecorder.start();
                console.log("WebGL麦克风启动");
            })
            .catch(err => console.error("WebGL麦克风启动失败: ", err));
    },
    
    WebGLStopMicrophone: function() {
        if (window.webglMediaRecorder) {
            window.webglMediaRecorder.stop();
            console.log("WebGL麦克风停止");
        }
    }
});