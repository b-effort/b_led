#define AUTOCONNECT_APKEY_SSID

#include <Arduino.h>

#include <AutoConnect.h>
#include <WebSocketsClient.h>

#include <FastLED.h>
#include <ESP32-HUB75-MatrixPanel-I2S-DMA.h>

#define WS_IP "192.168.86.100"
#define WS_PORT 42000
#define FIXTURE_ID "matrix_1"

#define BAUD_RATE 115200
#define PIN_BUTTON_DOWN 7

#define WIDTH 64
#define HEIGHT 64

const int NUM_LEDS = WIDTH * HEIGHT;

const HUB75_I2S_CFG::i2s_pins MATRIX_PINS = {
  .r1 = 42,
  .g1 = 41,
  .b1 = 40,
  .r2 = 38,
  .g2 = 39,
  .b2 = 37,
  .a = 45,
  .b = 36,
  .c = 48,
  .d = 35,
  .e = 21,
  .lat = 47,
  .oe = 14,
  .clk = 2,
};
const HUB75_I2S_CFG MATRIX_CONFIG(WIDTH, HEIGHT, 1, MATRIX_PINS);

MatrixPanel_I2S_DMA* display = nullptr;

WebSocketsClient ws;
typedef enum {
  WsMessageType_GetId = 0,
  WsMessageType_GetId_Reply = 1,
  WsMessageType_SetLEDs = 2,
} WsMessageType;

void onWsEvent(WStype_t, byte*, size_t);
void loadWiFiPortal();
void connectSavedWiFi();

void setup() {
  delay(1000);
  Serial.begin(BAUD_RATE);
  Serial.println();

  pinMode(PIN_BUTTON_DOWN, INPUT_PULLUP);

  connectSavedWiFi();
  Serial.println("WiFi Status: " + String(WiFi.status()));
  if (WiFi.status() == WL_CONNECTED)
    Serial.println("IP: " + WiFi.localIP().toString());

  display = new MatrixPanel_I2S_DMA(MATRIX_CONFIG);
  if (not display->begin())
      Serial.println("Display memory allocation failed!");
  display->setBrightness(220);
  Serial.println("display initialized");

  ws.begin(WS_IP, WS_PORT, "/");
  ws.onEvent(onWsEvent);
  ws.setReconnectInterval(1000);

  Serial.println("*** STARTED ***");
}

void loop() {
  if (digitalRead(PIN_BUTTON_DOWN) == LOW) {
    auto start = millis();
    while (digitalRead(PIN_BUTTON_DOWN) == LOW) {
      yield();
    }
    if (millis() - start > 2000) {
      Serial.println("loading wifi portal");
      loadWiFiPortal();
    }
  }

  ws.loop();
}

void setLEDs(byte* data, size_t length) {
  if (length != NUM_LEDS * 3) {
    Serial.println("ERROR: data length doesn't match number of leds. length=" + String(length));
    return;
  }

  for (int i = 0; i < NUM_LEDS; i++) {
    short x = i % WIDTH;
    short y = i / WIDTH;

    int offset = i * 3;
    byte r = *(data + offset);
    byte g = *(data + offset + 1);
    byte b = *(data + offset + 2);

    display->drawPixelRGB888(x, y, r, g, b);
  }
}

void onWsEvent(WStype_t type, byte* payload, size_t length) {
  switch (type) {
    case WStype_CONNECTED: {
      Serial.println("ws connected");
      break;
    }
    case WStype_DISCONNECTED: {
      Serial.println("ws disconnected");
      display->clearScreen();
      break;
    }
    case WStype_BIN: {
      WsMessageType msgType = (WsMessageType)*payload;
      byte *data = payload + 1;
      length -= 1;

      switch (msgType) {
        case WsMessageType_GetId: {
          Serial.println("sending fixture id " + String(FIXTURE_ID));
          ws.sendTXT(String(WsMessageType_GetId_Reply) + FIXTURE_ID);
          break;
        }
        case WsMessageType_SetLEDs: {
          setLEDs(data, length);
          break;
        }
      }
      break;
    }
  }
}

#pragma region wifi

#define WIFI_TIMEOUT 10000

void loadWiFiPortal() {
  static AutoConnectConfig ac_config;
  ac_config.immediateStart = true;

  static AutoConnect* ac_portal = nullptr;
  if (!ac_portal)
    ac_portal = new AutoConnect;

  ac_portal->config(ac_config);
  if (ac_portal->begin()) {
    ac_portal->end();
    delete ac_portal;
    ac_portal = nullptr;
  }
}

bool connectWiFi(const char* ssid, const char* password) {
  WiFi.begin(ssid, password);
  auto now = millis();
  while (WiFi.status() != WL_CONNECTED) {
    if (millis() - now > WIFI_TIMEOUT)
      return false;
  }
  return true;
}

void connectSavedWiFi() {
  WiFi.mode(WIFI_STA);
  delay(100);

  AutoConnectCredential cred;
  station_config_t staConfig;
  for (int8_t e = 0; e < cred.entries(); e++) {
    cred.load(e, &staConfig);
    if (connectWiFi((char*)staConfig.ssid, (char*)staConfig.password))
      return;
  }
}

#pragma endregion
