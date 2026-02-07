#!/bin/bash
# Custom file watcher for WPF development with Proton
# Usage: ./watch-dev.sh

PROJECT_DIR="~/wpf/MyWPFApp"
EXE_PATH="$PROJECT_DIR/bin/Debug/net10.0-windows/win-x64/MyWPFApp.exe"
STEAM_ROOT="${HOME}/.local/share/Steam"
PROTON_PATH="${STEAM_ROOT}/steamapps/common/Proton - Experimental"
PROTON_PREFIX="${HOME}/.proton"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

APP_PID=""

start_app() {
    echo -e "${GREEN}🚀 Starting WPF app...${NC}"
    STEAM_COMPAT_CLIENT_INSTALL_PATH="$STEAM_ROOT" \
    STEAM_COMPAT_DATA_PATH="$PROTON_PREFIX" \
    PROTON_USE_WINED3D=1 \
    "$PROTON_PATH/proton" run "$EXE_PATH" &>/dev/null &
    APP_PID=$!
    echo -e "${GREEN}✓ App started (PID: $APP_PID)${NC}"
}

stop_app() {
    if [ -n "$APP_PID" ]; then
        echo -e "${YELLOW}⏹ Stopping app...${NC}"
        pkill -f "MyWPFApp.exe" 2>/dev/null
        wait $APP_PID 2>/dev/null
        APP_PID=""
    fi
}

build_app() {
    echo -e "${CYAN}🔨 Building...${NC}"
    cd "$PROJECT_DIR"
    if dotnet build -v q 2>&1 | tail -5 | grep -q "0 Error"; then
        echo -e "${GREEN}✓ Build succeeded${NC}"
        return 0
    else
        echo -e "${RED}✗ Build failed${NC}"
        dotnet build 2>&1 | grep -E "error|Error" | head -10
        return 1
    fi
}

cleanup() {
    echo -e "\n${YELLOW}Shutting down...${NC}"
    stop_app
    exit 0
}

trap cleanup SIGINT SIGTERM

echo -e "${CYAN}═══════════════════════════════════════════${NC}"
echo -e "${CYAN}  WPF Development Watch Mode${NC}"
echo -e "${CYAN}═══════════════════════════════════════════${NC}"
echo -e "Watching: ${PROJECT_DIR}"
echo -e "Press ${YELLOW}Ctrl+C${NC} to exit"
echo -e "Press ${YELLOW}r${NC} + Enter to manually restart"
echo ""

# Initial build and start
if build_app; then
    start_app
fi

# Watch for file changes using inotifywait
if ! command -v inotifywait &> /dev/null; then
    echo -e "${RED}inotifywait not found. Install inotify-tools:${NC}"
    echo -e "  sudo pacman -S inotify-tools"
    echo ""
    echo -e "${YELLOW}Falling back to manual mode. Press 'r' + Enter to rebuild and restart.${NC}"
    
    while true; do
        read -t 1 -n 1 key
        if [ "$key" = "r" ]; then
            stop_app
            if build_app; then
                start_app
            fi
        fi
    done
else
    # Use inotifywait for file watching with debounce
    LAST_CHANGE=0
    DEBOUNCE_MS=1000
    
    inotifywait -m -r -e modify,create,delete \
        --include '.*\.(cs|xaml|csproj)$' \
        --exclude '.*(obj|bin|wpftmp).*' \
        "$PROJECT_DIR" 2>/dev/null | while read -r directory event filename; do
        
        # Skip obj, bin directories and temp files
        if [[ "$directory" == *"/obj/"* ]] || [[ "$directory" == *"/bin/"* ]] || [[ "$filename" == *"wpftmp"* ]]; then
            continue
        fi
        
        # Debounce - only trigger if more than DEBOUNCE_MS since last change
        CURRENT_TIME=$(date +%s%N | cut -b1-13)
        if (( CURRENT_TIME - LAST_CHANGE < DEBOUNCE_MS )); then
            continue
        fi
        LAST_CHANGE=$CURRENT_TIME
        
        echo -e "${YELLOW}⌚ File changed: ${filename}${NC}"
        stop_app
        sleep 0.5  # Brief pause to let file writes complete
        if build_app; then
            start_app
        fi
    done
fi
