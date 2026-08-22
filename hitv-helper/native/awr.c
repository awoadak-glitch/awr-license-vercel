#include <jni.h>

/* AWR HiTV helper: VIP gate + TraidMod update suppression without touching HiTV DEX/native code. */
static volatile jint g_vip_enabled = 0;

static jboolean JNICALL awrVipGate(JNIEnv *env, jobject thiz) {
    (void)env;
    (void)thiz;
    return g_vip_enabled ? JNI_TRUE : JNI_FALSE;
}

static void JNICALL awrNoopVoid(JNIEnv *env, jobject thiz) {
    (void)env;
    (void)thiz;
}

static void JNICALL awrNoopContext(JNIEnv *env, jobject thiz, jobject context) {
    (void)env;
    (void)thiz;
    (void)context;
}

static void JNICALL awrNoop3Strings(JNIEnv *env, jobject thiz, jstring a, jstring b, jstring c) {
    (void)env;
    (void)thiz;
    (void)a;
    (void)b;
    (void)c;
}

static jobject JNICALL awrNoopVoidArray(JNIEnv *env, jobject thiz, jobjectArray values) {
    (void)env;
    (void)thiz;
    (void)values;
    return NULL;
}

static void JNICALL awrNoopJson(JNIEnv *env, jobject thiz, jobject value) {
    (void)env;
    (void)thiz;
    (void)value;
}

static jboolean disableTraidUpdate(JNIEnv *env) {
    if (env == NULL) return JNI_FALSE;
    jboolean changed = JNI_FALSE;

    jclass upd = (*env)->FindClass(env, "com/extreme/modding/Upd8Chck");
    if (upd != NULL) {
        JNINativeMethod methods[] = {
            {"chk", "(Landroid/content/Context;)V", (void *)awrNoopContext},
            {"shw", "()V", (void *)awrNoopVoid},
            {"shwUpd8Dlg", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;)V", (void *)awrNoop3Strings},
            {"x", "()V", (void *)awrNoopVoid}
        };
        if ((*env)->RegisterNatives(env, upd, methods, 4) == 0) changed = JNI_TRUE;
        if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
        (*env)->DeleteLocalRef(env, upd);
    } else if ((*env)->ExceptionCheck(env)) {
        (*env)->ExceptionClear(env);
    }

    jclass task = (*env)->FindClass(env, "com/extreme/modding/Upd8Chck$CkUpd8Task");
    if (task != NULL) {
        JNINativeMethod taskMethods[] = {
            {"doInBackground", "([Ljava/lang/Void;)Lorg/json/JSONObject;", (void *)awrNoopVoidArray},
            {"onPostExecute", "(Lorg/json/JSONObject;)V", (void *)awrNoopJson}
        };
        if ((*env)->RegisterNatives(env, task, taskMethods, 2) == 0) changed = JNI_TRUE;
        if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
        (*env)->DeleteLocalRef(env, task);
    } else if ((*env)->ExceptionCheck(env)) {
        (*env)->ExceptionClear(env);
    }

    return changed;
}

JNIEXPORT jint JNICALL JNI_OnLoad(JavaVM *vm, void *reserved) {
    (void)reserved;
    JNIEnv *env = NULL;
    if (vm == NULL || (*vm)->GetEnv(vm, (void **)&env, JNI_VERSION_1_6) != JNI_OK) {
        return JNI_VERSION_1_6;
    }
    disableTraidUpdate(env);
    return JNI_VERSION_1_6;
}

JNIEXPORT jboolean JNICALL
Java_com_awr_license_AwrLicenseInitializationContentProviderForHiTV2026_nativeDisableTraidUpdate(
        JNIEnv *env, jclass clazz) {
    (void)clazz;
    return disableTraidUpdate(env);
}

JNIEXPORT jboolean JNICALL
Java_com_awr_license_AwrLicenseInitializationContentProviderForHiTV2026_nativeSetVipEnabled(
        JNIEnv *env, jclass clazz, jboolean enabled) {
    (void)clazz;
    if (env == NULL) return JNI_FALSE;

    disableTraidUpdate(env);
    g_vip_enabled = enabled ? 1 : 0;

    jclass userState = (*env)->FindClass(env, "com/hitv/venom/store/user/UserState");
    if (userState == NULL) {
        if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
        return JNI_FALSE;
    }

    JNINativeMethod methods[] = {
        {"isMemberValid", "()Z", (void *)awrVipGate},
        {"vipEnableAd", "()Z", (void *)awrVipGate},
        {"vipEnableDownloadFast", "()Z", (void *)awrVipGate},
        {"vipEnableDownloadHd", "()Z", (void *)awrVipGate},
        {"vipEnableDownloadParallel", "()Z", (void *)awrVipGate},
        {"vipEnablePaidVideo", "()Z", (void *)awrVipGate},
        {"vipEnablePlayHd", "()Z", (void *)awrVipGate},
        {"vipEnableProjection", "()Z", (void *)awrVipGate},
        {"vipEnableTV", "()Z", (void *)awrVipGate},
        {"vipEnableTogetherVoice", "()Z", (void *)awrVipGate}
    };

    jint count = (jint)(sizeof(methods) / sizeof(methods[0]));
    jint rc = (*env)->RegisterNatives(env, userState, methods, count);
    if (rc != 0) {
        if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
        (*env)->DeleteLocalRef(env, userState);
        return JNI_FALSE;
    }

    (*env)->DeleteLocalRef(env, userState);
    return JNI_TRUE;
}
