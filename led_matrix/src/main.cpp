#define AUTOCONNECT_APKEY_SSID

#include <Arduino.h>

#include <AutoConnect.h>
#include <WebSocketsClient.h>

#include <ESP32-HUB75-MatrixPanel-I2S-DMA.h>

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

#define WS_IP "192.168.86.100"
#define WS_PORT 42000
#define FIXTURE_ID "matrix_1"

#define BAUD_RATE 115200
#define PIN_BUTTON_UP 6
#define PIN_BUTTON_DOWN 7

#define WIDTH 64
#define HEIGHT 64

const int NUM_LEDS = WIDTH * HEIGHT;
const int LED_BUFFER_SIZE = NUM_LEDS * 3;
byte ledBuffer[LED_BUFFER_SIZE] = { 0 };

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

void task_web(void*);
void task_led(void*);
void task_input(void*);
TaskHandle_t* task_h_web;
TaskHandle_t* task_h_led;
TaskHandle_t* task_h_input;
EventGroupHandle_t eg;
#define EVENT_frameReady (1 << 0)

extern "C" void app_main() {
	initArduino();

	delay(1000);
	Serial.begin(BAUD_RATE);
	Serial.println();

	// *** pins
	pinMode(PIN_BUTTON_UP, INPUT_PULLUP);
	pinMode(PIN_BUTTON_DOWN, INPUT_PULLUP);

	// *** wifi
	esp_wifi_set_ps(WIFI_PS_NONE);
	connectSavedWiFi();
	Serial.print("WiFi Status: ");
	Serial.println(WiFi.status());
	if (WiFi.status() == WL_CONNECTED) {
		Serial.print("IP: ");
		Serial.println(WiFi.localIP().toString());
	}

	// *** display
	display = new MatrixPanel_I2S_DMA(MATRIX_CONFIG);
	if (not display->begin()) {
		Serial.println("ERROR: display memory allocation failed!");
		return;
	}
	display->setBrightness(220);
	display->clearScreen();
	Serial.println("display initialized");

	// *** tasks
	eg = xEventGroupCreate();
	// https://docs.espressif.com/projects/esp-idf/en/latest/esp32s3/api-guides/performance/speed.html#choosing-task-priorities-of-the-application
	xTaskCreatePinnedToCore(task_web, "task_web", 10000, nullptr, 5, task_h_web, 1);
	xTaskCreatePinnedToCore(task_led, "task_led", 10000, nullptr, 5, task_h_led, 0);
	// xTaskCreate(task_input, "task_input", 2048, nullptr, 2, task_h_input);

	Serial.println("*** STARTED ***");
}

#pragma region websockets

void onWsEvent(WStype_t, byte*, size_t);

void task_web(void* taskParams) {
	ws.begin(WS_IP, WS_PORT, "/");
	ws.onEvent(onWsEvent);
	ws.setReconnectInterval(1000);

	while (true) {
		auto now = micros();
		ws.loop();
		auto t = micros() - now;
		if (t > 10000) {
			Serial.print("ws.loop: ");
			Serial.println(t);
		}

		vTaskDelay(1);
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
					if (length != LED_BUFFER_SIZE) {
						Serial.println("ERROR: data length doesn't match number of leds. length=" + String(length));
						return;
					}
					memcpy(ledBuffer, data, length);

					xEventGroupSetBits(eg, EVENT_frameReady);
					break;
				}
			}
			break;
		}
	}
}

#pragma endregion

#pragma region leds

void setLEDs(byte*);

void task_led(void* taskParams) {
	while (true) {
		xEventGroupWaitBits(eg, EVENT_frameReady, pdTRUE, pdTRUE, portMAX_DELAY);

		// auto now = micros();
		// Serial.println("set leds: start");
		setLEDs(ledBuffer);
		// Serial.print("set leds: ");
		// Serial.println(micros() - now);
	}
}

void setLEDs(byte* data) {
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

#pragma endregion

#pragma region input/debug

void printMemory() {
	Serial.println("*** HEAP ***");
	Serial.print("total:     ");
	Serial.println(ESP.getHeapSize());
	Serial.print("free:      ");
	Serial.println(ESP.getFreeHeap());
	Serial.print("max alloc: ");
	Serial.println(ESP.getMaxAllocHeap());

	Serial.println("*** xTask HWM ***");
	Serial.print("task_web:  ");
	Serial.println(uxTaskGetStackHighWaterMark(task_h_web));
	Serial.print("task_led: ");
	Serial.println(uxTaskGetStackHighWaterMark(task_h_led));
}

bool buttonUpLast = false;
bool buttonDownLast = false;

void task_input(void* params) {
	while (true) {
		bool buttonUp = digitalRead(PIN_BUTTON_UP) == LOW;
		bool buttonDown = digitalRead(PIN_BUTTON_DOWN) == LOW;

		if (buttonUp && buttonUp != buttonUpLast) {
			printMemory();
		}

		if (buttonUp && buttonDown) {
			auto start = millis();
			while (digitalRead(PIN_BUTTON_UP)   == LOW
				&& digitalRead(PIN_BUTTON_DOWN) == LOW
			) {
				yield();
			}
			if (millis() - start > 2000) {
				Serial.println("loading wifi portal");
				loadWiFiPortal();
			}
		}

		buttonUpLast = buttonUp;
		buttonDownLast = buttonDown;

		vTaskDelay(1);
	}
}

#pragma endregion
