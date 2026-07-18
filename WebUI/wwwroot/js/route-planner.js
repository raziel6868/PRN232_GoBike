(() => {
  "use strict";

  const planner = document.getElementById("route-planner");
  if (!planner) return;

  const form = document.getElementById("route-form");
  const errorBox = document.getElementById("route-error");
  const resultPanel = document.getElementById("route-result");
  const loading = document.getElementById("map-loading");
  const mapMode = document.getElementById("map-mode");
  const submitButton = document.getElementById("compute-route");
  const routeSteps = document.getElementById("route-steps");
  const assistantPanel = document.getElementById("route-assistant");
  const assistantToggle = document.getElementById("route-assistant-toggle");
  const assistantForm = document.getElementById("route-assistant-form");
  const assistantInput = document.getElementById("route-assistant-input");
  const assistantMessages = document.getElementById("route-assistant-messages");
  const assistantSend = document.getElementById("route-assistant-send");
  const csrfToken = form.querySelector('input[name="__RequestVerificationToken"]').value;
  const mapTilerStyleUrl = planner.dataset.mapStyleUrl?.trim();
  const fields = {
    origin: createField("origin"),
    destination: createField("destination")
  };

  const state = {
    origin: null,
    destination: null,
    activeField: "origin",
    mapReady: false,
    markers: { origin: null, destination: null },
    searchControllers: { origin: null, destination: null },
    searchTimers: { origin: null, destination: null }
  };

  if (typeof maplibregl === "undefined") {
    showError("The map library could not be loaded.");
    return;
  }

  let usingFallbackMap = !mapTilerStyleUrl;

  const map = new maplibregl.Map({
    container: "route-map",
    center: [106.7009, 10.7769],
    zoom: 12,
    minZoom: 5,
    maxZoom: 19,
    style: mapTilerStyleUrl || createFallbackMapStyle()
  });

  map.addControl(new maplibregl.NavigationControl({ showCompass: false }), "top-right");

  map.on("style.load", initializeRouteLayers);

  map.on("error", () => {
    if (usingFallbackMap || state.mapReady) return;

    usingFallbackMap = true;
    map.setStyle(createFallbackMapStyle());
  });

  map.on("click", event => {
    const key = state.activeField;
    setPoint(key, {
      latitude: event.lngLat.lat,
      longitude: event.lngLat.lng,
      label: formatCoordinate(event.lngLat.lat, event.lngLat.lng)
    });
  });

  Object.entries(fields).forEach(([key, field]) => {
    field.input.addEventListener("focus", () => setActiveField(key));
    field.input.addEventListener("input", () => onLocationInput(key));
    field.input.addEventListener("keydown", event => {
      if (event.key === "Escape") hideSuggestions(field);
    });
    field.input.addEventListener("blur", () => {
      window.setTimeout(() => hideSuggestions(field), 160);
    });
  });

  document.getElementById("use-location").addEventListener("click", useCurrentLocation);
  document.getElementById("swap-locations").addEventListener("click", swapLocations);
  document.getElementById("route-assistant-close").addEventListener("click", closeAssistant);
  assistantToggle.addEventListener("click", toggleAssistant);
  assistantForm.addEventListener("submit", askAssistant);
  assistantInput.addEventListener("keydown", event => {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      assistantForm.requestSubmit();
    }
  });
  form.addEventListener("submit", computeRoute);

  function toggleAssistant() {
    if (assistantPanel.hidden) {
      assistantPanel.hidden = false;
      assistantToggle.setAttribute("aria-expanded", "true");
      assistantInput.focus();
      scrollAssistantToBottom();
    } else {
      closeAssistant();
    }
  }

  function closeAssistant() {
    assistantPanel.hidden = true;
    assistantToggle.setAttribute("aria-expanded", "false");
  }

  async function askAssistant(event) {
    event.preventDefault();
    const message = assistantInput.value.trim();
    if (message.length < 3) return;

    appendChatMessage(message, "user");
    assistantInput.value = "";
    setAssistantLoading(true);

    try {
      const center = await getAssistantSearchCenter();
      const response = await fetch(planner.dataset.assistantUrl, {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
          RequestVerificationToken: csrfToken
        },
        body: JSON.stringify({
          message,
          latitude: center.latitude,
          longitude: center.longitude
        })
      });

      if (!response.ok) throw new Error(await readError(response));
      const payload = await response.json();
      appendChatMessage(payload.assistantMessage, "assistant");
      renderAssistantPlaces(payload.places || []);
    } catch (error) {
      appendChatMessage(error.message || "I could not search nearby places right now.", "assistant error");
    } finally {
      setAssistantLoading(false);
      assistantInput.focus();
    }
  }

  async function getAssistantSearchCenter() {
    if (state.origin) return state.origin;

    try {
      const position = await locateCurrentPosition();
      const currentLocation = {
        latitude: position.coords.latitude,
        longitude: position.coords.longitude,
        label: "Current location"
      };
      setPoint("origin", currentLocation);
      return currentLocation;
    } catch {
      const center = map.getCenter();
      appendChatMessage("Location access was unavailable, so I searched around the visible map area.", "assistant note");
      return { latitude: center.lat, longitude: center.lng };
    }
  }

  function appendChatMessage(message, className) {
    const bubble = document.createElement("div");
    bubble.className = `route-chat-message ${className}`;
    bubble.textContent = message;
    assistantMessages.appendChild(bubble);
    scrollAssistantToBottom();
  }

  function renderAssistantPlaces(places) {
    if (!Array.isArray(places) || places.length === 0) return;

    const results = document.createElement("div");
    results.className = "route-chat-results";

    places.forEach(place => {
      const option = document.createElement("button");
      option.type = "button";
      option.className = "route-chat-place";

      const title = document.createElement("strong");
      title.textContent = place.name || place.label;
      const detail = document.createElement("span");
      detail.textContent = place.label;
      option.append(title, detail);

      if (place.distanceMeters != null) {
        const distance = document.createElement("small");
        distance.textContent = formatDistance(place.distanceMeters);
        option.appendChild(distance);
      }

      option.addEventListener("click", async () => {
        setPoint("destination", place);
        closeAssistant();
        if (state.origin) {
          await calculateRoute();
        } else {
          setActiveField("origin");
          fields.origin.input.focus();
          showError("Set an origin before calculating directions to this place.");
        }
      });
      results.appendChild(option);
    });

    assistantMessages.appendChild(results);
    scrollAssistantToBottom();
  }

  function setAssistantLoading(isLoading) {
    assistantInput.disabled = isLoading;
    assistantSend.disabled = isLoading;
    assistantSend.textContent = isLoading ? "Searching..." : "Send";
  }

  function scrollAssistantToBottom() {
    assistantMessages.scrollTop = assistantMessages.scrollHeight;
  }

  function createField(key) {
    const container = planner.querySelector(`[data-location-field="${key}"]`);
    return {
      container,
      input: container.querySelector("input"),
      suggestions: container.querySelector(".route-suggestions")
    };
  }

  function setActiveField(key) {
    state.activeField = key;
    mapMode.textContent = key === "origin" ? "Set origin" : "Set destination";
  }

  function onLocationInput(key) {
    const field = fields[key];
    setActiveField(key);

    if (state[key] && field.input.value !== state[key].label) {
      state[key] = null;
      syncMarker(key);
      clearComputedRoute();
      updateSubmitState();
    }

    window.clearTimeout(state.searchTimers[key]);
    state.searchControllers[key]?.abort();

    const query = field.input.value.trim();
    if (query.length < 3) {
      hideSuggestions(field);
      return;
    }

    state.searchTimers[key] = window.setTimeout(() => searchPlaces(key, query), 350);
  }

  async function searchPlaces(key, query) {
    const field = fields[key];
    const controller = new AbortController();
    state.searchControllers[key] = controller;

    try {
      const url = new URL(planner.dataset.searchUrl, window.location.origin);
      url.searchParams.set("query", query);
      const response = await fetch(url, {
        signal: controller.signal,
        headers: { Accept: "application/json" }
      });
      if (!response.ok) throw new Error(await readError(response));

      const places = await response.json();
      renderSuggestions(key, places);
    } catch (error) {
      if (error.name !== "AbortError") showError(error.message || "Location search failed.");
    }
  }

  function renderSuggestions(key, places) {
    const field = fields[key];
    field.suggestions.replaceChildren();

    if (!Array.isArray(places) || places.length === 0) {
      const empty = document.createElement("div");
      empty.className = "route-suggestion";
      empty.textContent = "No locations found";
      field.suggestions.appendChild(empty);
    } else {
      places.forEach(place => {
        const option = document.createElement("button");
        option.type = "button";
        option.className = "route-suggestion";
        option.setAttribute("role", "option");

        const title = document.createElement("strong");
        title.textContent = place.name || place.label;
        const detail = document.createElement("span");
        detail.textContent = place.label;
        option.append(title, detail);
        option.addEventListener("mousedown", event => event.preventDefault());
        option.addEventListener("click", () => {
          setPoint(key, place);
          hideSuggestions(field);
        });
        field.suggestions.appendChild(option);
      });
    }

    field.suggestions.hidden = false;
  }

  function hideSuggestions(field) {
    field.suggestions.hidden = true;
  }

  function setPoint(key, point) {
    state[key] = {
      latitude: Number(point.latitude),
      longitude: Number(point.longitude),
      label: point.label || formatCoordinate(point.latitude, point.longitude)
    };
    fields[key].input.value = state[key].label;
    syncMarker(key);
    clearComputedRoute();
    clearError();

    if (key === "origin" && !state.destination) {
      setActiveField("destination");
      fields.destination.input.focus();
    }

    updateSubmitState();
  }

  function syncMarker(key) {
    state.markers[key]?.remove();
    state.markers[key] = null;
    if (!state[key]) return;

    const element = document.createElement("div");
    element.className = `route-map-marker ${key}`;
    element.setAttribute("aria-label", key === "origin" ? "Origin" : "Destination");

    state.markers[key] = new maplibregl.Marker({ element, draggable: true })
      .setLngLat([state[key].longitude, state[key].latitude])
      .addTo(map);

    state.markers[key].on("dragend", () => {
      const position = state.markers[key].getLngLat();
      state[key] = {
        latitude: position.lat,
        longitude: position.lng,
        label: formatCoordinate(position.lat, position.lng)
      };
      fields[key].input.value = state[key].label;
      clearComputedRoute();
    });

    map.easeTo({ center: [state[key].longitude, state[key].latitude], duration: 450 });
  }

  function useCurrentLocation() {
    clearError();
    if (!navigator.geolocation) {
      showError("Location services are not supported by this browser.");
      return;
    }

    const button = document.getElementById("use-location");
    button.disabled = true;
    button.textContent = "Locating...";
    locateCurrentPosition().then(
      position => {
        button.disabled = false;
        button.textContent = "Current location";
        setPoint("origin", {
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          label: "Current location"
        });
        map.flyTo({
          center: [position.coords.longitude, position.coords.latitude],
          zoom: 15,
          duration: 700
        });
      },
      () => {
        button.disabled = false;
        button.textContent = "Current location";
        showError("Location permission was not granted.");
      }
    );
  }

  function locateCurrentPosition() {
    return new Promise((resolve, reject) => {
      if (!navigator.geolocation) {
        reject(new Error("Location services are not supported by this browser."));
        return;
      }

      navigator.geolocation.getCurrentPosition(resolve, reject, {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 30000
      });
    });
  }

  function swapLocations() {
    [state.origin, state.destination] = [state.destination, state.origin];
    fields.origin.input.value = state.origin?.label || "";
    fields.destination.input.value = state.destination?.label || "";
    syncMarker("origin");
    syncMarker("destination");
    clearComputedRoute();
    setActiveField(state.origin ? "destination" : "origin");
    updateSubmitState();
  }

  async function computeRoute(event) {
    event.preventDefault();
    await calculateRoute();
  }

  async function calculateRoute() {
    if (!state.origin || !state.destination || !state.mapReady) return;

    clearError();
    setLoading(true);
    try {
      const response = await fetch(planner.dataset.computeUrl, {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
          RequestVerificationToken: csrfToken
        },
        body: JSON.stringify({
          origin: state.origin,
          destination: state.destination,
          avoidHighways: document.getElementById("avoid-highways").checked,
          avoidTolls: document.getElementById("avoid-tolls").checked,
          avoidFerries: document.getElementById("avoid-ferries").checked
        })
      });

      if (!response.ok) throw new Error(await readError(response));
      const route = await response.json();
      renderRoute(route);
    } catch (error) {
      showError(error.message || "Route calculation failed.");
    } finally {
      setLoading(false);
    }
  }

  function renderRoute(route) {
    const coordinates = Array.isArray(route.coordinates) ? route.coordinates : [];
    const source = map.getSource("route-line");
    if (!source) {
      showError("The map is not ready yet. Please try again.");
      return;
    }

    source.setData({
      type: "Feature",
      properties: {},
      geometry: { type: "LineString", coordinates }
    });

    if (Array.isArray(route.boundingBox) && route.boundingBox.length === 4) {
      map.fitBounds(
        [[route.boundingBox[0], route.boundingBox[1]], [route.boundingBox[2], route.boundingBox[3]]],
        { padding: 64, duration: 700 }
      );
    }

    document.getElementById("route-distance").textContent = formatDistance(route.distanceMeters);
    document.getElementById("route-duration").textContent = formatDuration(route.durationSeconds);
    renderSteps(route.steps || []);
    updateNavigationLink();
    resultPanel.hidden = false;
  }

  function renderSteps(steps) {
    routeSteps.replaceChildren();
    const fragment = document.createDocumentFragment();

    steps.forEach((step, index) => {
      const item = document.createElement("li");
      const number = document.createElement("span");
      number.className = "route-step-number";
      number.textContent = String(index + 1);
      const instruction = document.createElement("span");
      instruction.className = "route-step-instruction";
      instruction.textContent = step.instruction || "Continue";
      const distance = document.createElement("span");
      distance.className = "route-step-distance";
      distance.textContent = formatDistance(step.distanceMeters);
      item.append(number, instruction, distance);
      fragment.appendChild(item);
    });

    routeSteps.appendChild(fragment);
    document.getElementById("route-step-count").textContent = `${steps.length} steps`;
  }

  function updateNavigationLink() {
    const url = new URL("https://www.google.com/maps/dir/");
    url.searchParams.set("api", "1");
    url.searchParams.set("origin", `${state.origin.latitude},${state.origin.longitude}`);
    url.searchParams.set("destination", `${state.destination.latitude},${state.destination.longitude}`);
    url.searchParams.set("travelmode", "driving");
    document.getElementById("open-navigation").href = url.toString();
  }

  function clearComputedRoute() {
    resultPanel.hidden = true;
    const source = state.mapReady ? map.getSource("route-line") : null;
    if (source) source.setData(emptyRoute());
  }

  function initializeRouteLayers() {
    if (!map.getSource("route-line")) {
      map.addSource("route-line", {
        type: "geojson",
        data: emptyRoute()
      });
    }

    if (!map.getLayer("route-line-casing")) {
      map.addLayer({
        id: "route-line-casing",
        type: "line",
        source: "route-line",
        layout: { "line-cap": "round", "line-join": "round" },
        paint: { "line-color": "#ffffff", "line-width": 9, "line-opacity": 0.92 }
      });
    }

    if (!map.getLayer("route-line-main")) {
      map.addLayer({
        id: "route-line-main",
        type: "line",
        source: "route-line",
        layout: { "line-cap": "round", "line-join": "round" },
        paint: { "line-color": "#1f6feb", "line-width": 5 }
      });
    }

    state.mapReady = true;
    updateSubmitState();
  }

  function createFallbackMapStyle() {
    return {
      version: 8,
      sources: {
        osm: {
          type: "raster",
          tiles: ["https://tile.openstreetmap.org/{z}/{x}/{y}.png"],
          tileSize: 256,
          attribution: "&copy; OpenStreetMap contributors"
        }
      },
      layers: [{ id: "osm", type: "raster", source: "osm" }]
    };
  }

  function emptyRoute() {
    return { type: "FeatureCollection", features: [] };
  }

  function updateSubmitState() {
    submitButton.disabled = !state.origin || !state.destination || !state.mapReady;
  }

  function setLoading(isLoading) {
    loading.hidden = !isLoading;
    submitButton.disabled = isLoading || !state.origin || !state.destination || !state.mapReady;
    submitButton.textContent = isLoading ? "Finding route..." : "Find route";
  }

  function showError(message) {
    errorBox.textContent = message;
    errorBox.hidden = false;
  }

  function clearError() {
    errorBox.hidden = true;
    errorBox.textContent = "";
  }

  async function readError(response) {
    try {
      const payload = await response.json();
      return payload.message || `Request failed with HTTP ${response.status}.`;
    } catch {
      return `Request failed with HTTP ${response.status}.`;
    }
  }

  function formatCoordinate(latitude, longitude) {
    return `${Number(latitude).toFixed(5)}, ${Number(longitude).toFixed(5)}`;
  }

  function formatDistance(meters) {
    const value = Number(meters) || 0;
    return value >= 1000 ? `${(value / 1000).toFixed(1)} km` : `${Math.round(value)} m`;
  }

  function formatDuration(seconds) {
    const minutes = Math.max(1, Math.round((Number(seconds) || 0) / 60));
    if (minutes < 60) return `${minutes} min`;
    const hours = Math.floor(minutes / 60);
    const remainingMinutes = minutes % 60;
    return remainingMinutes ? `${hours} hr ${remainingMinutes} min` : `${hours} hr`;
  }
})();
