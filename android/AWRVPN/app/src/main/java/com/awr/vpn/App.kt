package com.awr.vpn

import android.content.Context
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder
import android.app.Activity
import android.content.Intent
import android.net.VpnService
import de.blinkt.openvpn.VpnProfile
import de.blinkt.openvpn.core.ConfigParser
import de.blinkt.openvpn.core.OpenVPNService
import de.blinkt.openvpn.core.ProfileManager
import de.blinkt.openvpn.core.VPNLaunchHelper
import java.io.StringReader
import android.app.AlertDialog
import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.os.Bundle
import android.provider.Settings
import android.text.InputType
import android.view.Gravity
import android.view.View
import android.widget.*
import de.blinkt.openvpn.core.ConnectionStatus
import de.blinkt.openvpn.core.VpnStatus
import java.util.Locale


data class VpnLocation(
    val code: String,
    val country: String,
    val flag: String,
    val vip: Boolean = false
)

data class VpnProfileData(
    val name: String,
    val country: String,
    val city: String,
    val ovpn: String
)

enum class VpnProtocol(val label: String) { AUTO("Auto"), UDP("UDP"), TCP("TCP") }
enum class DnsMode(val label: String, val dns1: String, val dns2: String) {
    CLOUDFLARE("Cloudflare", "1.1.1.1", "1.0.0.1"),
    GOOGLE("Google", "8.8.8.8", "8.8.4.4"),
    ADGUARD("AdGuard", "94.140.14.14", "94.140.15.15")
}

object Locations {
    val all = listOf(
        VpnLocation("AUTO", "Fastest server", "⚡"),
        VpnLocation("SG", "Singapore", "🇸🇬"), VpnLocation("SA", "Saudi Arabia", "🇸🇦"),
        VpnLocation("AU", "Australia", "🇦🇺"), VpnLocation("AT", "Austria", "🇦🇹"),
        VpnLocation("CA", "Canada", "🇨🇦"), VpnLocation("DK", "Denmark", "🇩🇰"),
        VpnLocation("FI", "Finland", "🇫🇮"), VpnLocation("FR", "France", "🇫🇷"),
        VpnLocation("DE", "Germany", "🇩🇪"), VpnLocation("IE", "Ireland", "🇮🇪"),
        VpnLocation("NL", "Netherlands", "🇳🇱"), VpnLocation("NZ", "New Zealand", "🇳🇿"),
        VpnLocation("NO", "Norway", "🇳🇴"), VpnLocation("CH", "Switzerland", "🇨🇭"),
        VpnLocation("UK", "United Kingdom", "🇬🇧"), VpnLocation("US", "United States", "🇺🇸"),
        VpnLocation("IL", "Israel", "🇮🇱"), VpnLocation("BE", "Belgium", "🇧🇪"),
        VpnLocation("LU", "Luxembourg", "🇱🇺"), VpnLocation("SE", "Sweden", "🇸🇪"),
        VpnLocation("AE", "United Arab Emirates", "🇦🇪", true), VpnLocation("MC", "Monaco", "🇲🇨", true),
        VpnLocation("LI", "Liechtenstein", "🇱🇮", true), VpnLocation("HK", "Hong Kong", "🇭🇰"),
        VpnLocation("SB", "Solomon Islands", "🇸🇧", true), VpnLocation("MR", "Mauritania", "🇲🇷", true),
        VpnLocation("MT", "Malta", "🇲🇹", true)
    )
}

object AwrApi {
    private const val BASE = "https://awr-license-vercel.vercel.app"
    data class VipResult(val valid: Boolean, val expiresAt: String?, val message: String)

    fun verifyVip(key: String): VipResult {
        return try {
            val conn = URL("$BASE/api/verify").openConnection() as HttpURLConnection
            conn.requestMethod = "POST"
            conn.connectTimeout = 12000
            conn.readTimeout = 12000
            conn.doOutput = true
            conn.setRequestProperty("Content-Type", "application/json; charset=utf-8")
            val body = JSONObject().put("key", key.trim()).toString()
            conn.outputStream.use { it.write(body.toByteArray(Charsets.UTF_8)) }
            val text = (if (conn.responseCode in 200..299) conn.inputStream else conn.errorStream)
                ?.bufferedReader()?.use { it.readText() }.orEmpty()
            val obj = JSONObject(text.ifBlank { "{}" })
            if (obj.optBoolean("success", false) && obj.optString("auth") == "AWR_OK_2026") {
                VipResult(true, obj.optString("expires_at").takeIf { it.isNotBlank() && it != "null" }, "VIP activated")
            } else {
                VipResult(false, null, obj.optString("code", "Invalid code"))
            }
        } catch (e: Exception) {
            VipResult(false, null, "Connection error: ${e.message ?: "unknown"}")
        }
    }
}

class VipStore(context: Context) {
    private val prefs = context.getSharedPreferences("awr_vip", Context.MODE_PRIVATE)
    fun isVip(): Boolean = prefs.getBoolean("active", false)
    fun code(): String = prefs.getString("code", "") ?: ""
    fun save(code: String, expiresAt: String?) {
        prefs.edit().putBoolean("active", true).putString("code", code).putString("expires", expiresAt).apply()
    }
    fun clear() = prefs.edit().clear().apply()
}

object ServerRepository {
    private const val ENDPOINT = "https://awr-license-vercel.vercel.app/api/vpn-servers"

    fun fetch(location: VpnLocation, protocol: VpnProtocol, dns: DnsMode, vipCode: String?): Result<VpnProfileData> = runCatching {
        val q = buildString {
            append("?country=").append(URLEncoder.encode(location.code, "UTF-8"))
            append("&protocol=").append(URLEncoder.encode(protocol.name.lowercase(), "UTF-8"))
        }
        val conn = URL(ENDPOINT + q).openConnection() as HttpURLConnection
        conn.requestMethod = "GET"
        conn.connectTimeout = 15000
        conn.readTimeout = 20000
        conn.setRequestProperty("Accept", "application/json")
        if (!vipCode.isNullOrBlank()) conn.setRequestProperty("X-AWR-VIP", vipCode)
        val text = (if (conn.responseCode in 200..299) conn.inputStream else conn.errorStream)
            ?.bufferedReader()?.use { it.readText() }.orEmpty()
        val obj = JSONObject(text.ifBlank { "{}" })
        if (!obj.optBoolean("success", false)) error(obj.optString("message", "No server available"))
        var config = obj.getString("ovpn")
        config += "\ndhcp-option DNS ${dns.dns1}\ndhcp-option DNS ${dns.dns2}\n"
        VpnProfileData(
            obj.optString("name", "AWR ${location.country}"),
            obj.optString("country", location.code),
            obj.optString("city", ""),
            config
        )
    }
}

class VpnEngine(private val context: Context) {
    private var pendingProfile: VpnProfile? = null

    fun prepare(profileData: VpnProfileData): Intent? {
        val parser = ConfigParser()
        parser.parseConfig(StringReader(profileData.ovpn))
        val profile = parser.convertProfile()
        profile.mName = profileData.name
        ProfileManager.setTemporaryProfile(context, profile)
        pendingProfile = profile
        return VpnService.prepare(context)
    }

    fun startPrepared() {
        pendingProfile?.let { VPNLaunchHelper.startOpenVpn(it, context) }
    }

    fun disconnect() {
        val intent = Intent(context, OpenVPNService::class.java)
        intent.action = OpenVPNService.DISCONNECT_VPN
        if (android.os.Build.VERSION.SDK_INT >= 26) context.startForegroundService(intent) else context.startService(intent)
    }
}

class MainActivity : Activity(), VpnStatus.StateListener {
    private lateinit var engine: VpnEngine
    private lateinit var vip: VipStore
    private lateinit var statusText: TextView
    private lateinit var connectButton: TextView
    private lateinit var locationText: TextView
    private lateinit var vipText: TextView
    private lateinit var protocolText: TextView
    private lateinit var dnsText: TextView

    private var selected = Locations.all.first()
    private var protocol = VpnProtocol.AUTO
    private var dns = DnsMode.CLOUDFLARE
    private var connected = false

    private val bg = Color.rgb(7,17,31)
    private val card = Color.rgb(14,30,48)
    private val mint = Color.rgb(0,229,168)
    private val muted = Color.rgb(151,169,187)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        engine = VpnEngine(this)
        vip = VipStore(this)
        buildUi()
        refreshVipLabel()
    }

    override fun onResume() {
        super.onResume()
        VpnStatus.addStateListener(this)
    }

    override fun onPause() {
        VpnStatus.removeStateListener(this)
        super.onPause()
    }

    private fun buildUi() {
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(bg)
            setPadding(dp(20), dp(18), dp(20), dp(26))
        }
        val scroll = ScrollView(this).apply { setBackgroundColor(bg); addView(root) }
        setContentView(scroll)

        val top = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL; gravity = Gravity.CENTER_VERTICAL }
        top.addView(TextView(this).apply {
            text = "AWR VPN"; textSize = 26f; setTextColor(Color.WHITE); setTypeface(typeface, Typeface.BOLD)
        }, LinearLayout.LayoutParams(0, -2, 1f))
        vipText = pill(if (vip.isVip()) "VIP ACTIVE" else "AWR-VIP", mint).apply { setOnClickListener { showVipDialog() } }
        top.addView(vipText)
        root.addView(top)

        root.addView(TextView(this).apply {
            text = "Private • Fast • Borderless"; textSize = 13f; setTextColor(muted); setPadding(0, dp(4), 0, dp(22))
        })

        val statusCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL; gravity = Gravity.CENTER; setPadding(dp(18), dp(26), dp(18), dp(26)); background = rounded(card, 26f)
        }
        statusText = TextView(this).apply {
            text = "Ready to connect"; textSize = 16f; gravity = Gravity.CENTER; setTextColor(muted)
        }
        statusCard.addView(statusText)
        connectButton = TextView(this).apply {
            text = "CONNECT"; textSize = 22f; gravity = Gravity.CENTER; setTypeface(typeface, Typeface.BOLD); setTextColor(bg)
            background = rounded(mint, 90f); setPadding(dp(28), dp(42), dp(28), dp(42)); setOnClickListener { toggleConnection() }
        }
        statusCard.addView(connectButton, LinearLayout.LayoutParams(dp(190), dp(190)).apply { topMargin = dp(20); gravity = Gravity.CENTER })
        locationText = TextView(this).apply {
            text = "⚡  Fastest server"; textSize = 17f; setTextColor(Color.WHITE); gravity = Gravity.CENTER
            setPadding(dp(12), dp(20), dp(12), dp(8)); setOnClickListener { showLocationDialog() }
        }
        statusCard.addView(locationText)
        root.addView(statusCard, LinearLayout.LayoutParams(-1, -2).apply { bottomMargin = dp(18) })

        val quick = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        protocolText = quickCard("Protocol", protocol.label) { showProtocolDialog() }
        dnsText = quickCard("DNS", dns.label) { showDnsDialog() }
        quick.addView(protocolText, LinearLayout.LayoutParams(0, dp(86), 1f).apply { rightMargin = dp(8) })
        quick.addView(dnsText, LinearLayout.LayoutParams(0, dp(86), 1f).apply { leftMargin = dp(8) })
        root.addView(quick)

        root.addView(sectionTitle("SECURITY"))
        root.addView(settingRow("Kill Switch", "Use Android Always-on VPN / block without VPN") {
            startActivity(Intent(Settings.ACTION_VPN_SETTINGS))
        })
        root.addView(settingRow("AWR-VIP", "Activate a code paid by transfer") { showVipDialog() })
        root.addView(settingRow("Server catalog", "VPN regions managed by AWR") { showLocationDialog() })
        root.addView(settingRow("Connection mode", "OpenVPN • UDP/TCP • secure DNS") { showProtocolDialog() })
    }

    private fun toggleConnection() {
        if (connected) { engine.disconnect(); return }
        if (selected.vip && !vip.isVip()) { showVipDialog(); return }
        statusText.text = "Finding the best route…"
        connectButton.isEnabled = false
        Thread {
            val result = ServerRepository.fetch(selected, protocol, dns, vip.code().takeIf { vip.isVip() })
            runOnUiThread {
                connectButton.isEnabled = true
                result.onSuccess { data ->
                    try {
                        val permission = engine.prepare(data)
                        if (permission != null) startActivityForResult(permission, 7001) else engine.startPrepared()
                    } catch (e: Exception) {
                        statusText.text = "Profile error: ${e.message ?: "invalid config"}"
                    }
                }.onFailure { e -> statusText.text = e.message ?: "Server unavailable" }
            }
        }.start()
    }

    @Deprecated("Deprecated in Android")
    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        if (requestCode == 7001 && resultCode == RESULT_OK) engine.startPrepared()
    }

    private fun showLocationDialog() {
        val items = Locations.all.map { "${it.flag}  ${it.country}${if (it.vip) "   ★ VIP" else ""}" }.toTypedArray()
        AlertDialog.Builder(this).setTitle("Choose server").setSingleChoiceItems(items, Locations.all.indexOf(selected)) { d, which ->
            val candidate = Locations.all[which]
            if (candidate.vip && !vip.isVip()) { d.dismiss(); showVipDialog() }
            else { selected = candidate; locationText.text = "${candidate.flag}  ${candidate.country}"; d.dismiss() }
        }.setNegativeButton("Cancel", null).show()
    }

    private fun showProtocolDialog() {
        val values = VpnProtocol.values()
        AlertDialog.Builder(this).setTitle("VPN protocol").setSingleChoiceItems(values.map { it.label }.toTypedArray(), values.indexOf(protocol)) { d, w ->
            protocol = values[w]; protocolText.text = "Protocol\n${protocol.label}"; d.dismiss()
        }.show()
    }

    private fun showDnsDialog() {
        val values = DnsMode.values()
        AlertDialog.Builder(this).setTitle("Secure DNS").setSingleChoiceItems(values.map { "${it.label}  ${it.dns1}" }.toTypedArray(), values.indexOf(dns)) { d, w ->
            dns = values[w]; dnsText.text = "DNS\n${dns.label}"; d.dismiss()
        }.show()
    }

    private fun showVipDialog() {
        val input = EditText(this).apply {
            hint = "AWR-XXXX-XXXX-XXXX"; inputType = InputType.TYPE_CLASS_TEXT; setSingleLine(true); setPadding(dp(18), dp(14), dp(18), dp(14))
        }
        val wrap = FrameLayout(this).apply { setPadding(dp(22), 0, dp(22), 0); addView(input) }
        val dialog = AlertDialog.Builder(this).setTitle("AWR-VIP").setMessage("Enter the activation code after payment by transfer.")
            .setView(wrap).setNegativeButton("Cancel", null).setPositiveButton("Activate", null).create()
        dialog.setOnShowListener {
            dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener {
                val key = input.text.toString().trim()
                if (key.isBlank()) { input.error = "Enter a code"; return@setOnClickListener }
                dialog.getButton(AlertDialog.BUTTON_POSITIVE).isEnabled = false
                Thread {
                    val res = AwrApi.verifyVip(key)
                    runOnUiThread {
                        dialog.getButton(AlertDialog.BUTTON_POSITIVE).isEnabled = true
                        if (res.valid) { vip.save(key, res.expiresAt); refreshVipLabel(); Toast.makeText(this, "AWR-VIP activated", Toast.LENGTH_LONG).show(); dialog.dismiss() }
                        else input.error = res.message
                    }
                }.start()
            }
        }
        dialog.show()
    }

    private fun refreshVipLabel() {
        if (::vipText.isInitialized) vipText.text = if (vip.isVip()) "VIP ACTIVE" else "AWR-VIP"
    }

    override fun updateState(state: String?, logmessage: String?, localizedResId: Int, level: ConnectionStatus?, intent: Intent?) {
        runOnUiThread {
            val s = (state ?: "").uppercase(Locale.ROOT)
            connected = s.contains("CONNECTED") && !s.contains("NOTCONNECTED")
            statusText.text = when {
                connected -> "Protected • ${selected.country}"
                s.contains("AUTH") -> "Authenticating…"
                s.contains("WAIT") || s.contains("CONNECT") -> "Connecting…"
                else -> logmessage?.takeIf { it.isNotBlank() } ?: "Ready to connect"
            }
            connectButton.text = if (connected) "DISCONNECT" else "CONNECT"
        }
    }

    override fun setConnectedVPN(uuid: String?) = Unit

    private fun pill(textValue: String, color: Int) = TextView(this).apply {
        text = textValue; textSize = 12f; setTypeface(typeface, Typeface.BOLD); setTextColor(bg); gravity = Gravity.CENTER
        setPadding(dp(14), dp(8), dp(14), dp(8)); background = rounded(color, 30f)
    }

    private fun quickCard(title: String, value: String, click: () -> Unit) = TextView(this).apply {
        text = "$title\n$value"; textSize = 15f; setTextColor(Color.WHITE); gravity = Gravity.CENTER; background = rounded(card, 18f); setOnClickListener { click() }
    }

    private fun sectionTitle(t: String) = TextView(this).apply {
        text = t; textSize = 12f; setTypeface(typeface, Typeface.BOLD); setTextColor(muted); setPadding(0, dp(26), 0, dp(10))
    }

    private fun settingRow(title: String, sub: String, click: () -> Unit): View {
        return LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL; setPadding(dp(18), dp(15), dp(18), dp(15)); background = rounded(card, 16f); setOnClickListener { click() }
            addView(TextView(this@MainActivity).apply { text = title; textSize = 16f; setTextColor(Color.WHITE); setTypeface(typeface, Typeface.BOLD) })
            addView(TextView(this@MainActivity).apply { text = sub; textSize = 12f; setTextColor(muted); setPadding(0, dp(4), 0, 0) })
            layoutParams = LinearLayout.LayoutParams(-1, -2).apply { bottomMargin = dp(10) }
        }
    }

    private fun rounded(color: Int, radius: Float) = GradientDrawable().apply { setColor(color); cornerRadius = dp(radius.toInt()).toFloat() }
    private fun dp(v: Int) = (v * resources.displayMetrics.density).toInt()
}
