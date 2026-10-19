package com.coup.security;
import android.content.Context;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;
import java.security.KeyStore;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;
public final class SessionVault {
    private static final String ALIAS = "coup.refresh.v1";
    private static SecretKey key() throws Exception {
        KeyStore store = KeyStore.getInstance("AndroidKeyStore"); store.load(null);
        if (!store.containsAlias(ALIAS)) {
            KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
            generator.init(new KeyGenParameterSpec.Builder(ALIAS, KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build());
            generator.generateKey();
        }
        return (SecretKey)store.getKey(ALIAS, null);
    }
    public static void write(Context context, String text) throws Exception {
        android.content.SharedPreferences.Editor edit = context.getSharedPreferences(ALIAS, Context.MODE_PRIVATE).edit();
        if (text == null || text.isEmpty()) { if (!edit.clear().commit()) throw new Exception("Secure storage failed"); return; }
        Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding"); cipher.init(Cipher.ENCRYPT_MODE, key());
        String value = Base64.encodeToString(cipher.getIV(), Base64.NO_WRAP) + ":" + Base64.encodeToString(cipher.doFinal(text.getBytes("UTF-8")), Base64.NO_WRAP);
        if (!edit.putString("value", value).commit()) throw new Exception("Secure storage failed");
    }
    public static String read(Context context) throws Exception {
        String value = context.getSharedPreferences(ALIAS, Context.MODE_PRIVATE).getString("value", null);
        if (value == null) return null;
        String[] parts = value.split(":");
        Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
        cipher.init(Cipher.DECRYPT_MODE, key(), new GCMParameterSpec(128, Base64.decode(parts[0], Base64.NO_WRAP)));
        return new String(cipher.doFinal(Base64.decode(parts[1], Base64.NO_WRAP)), "UTF-8");
    }
}
