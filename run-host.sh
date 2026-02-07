#!/bin/bash
# WPF Hot Reload Development Environment
# Launches the WpfHost which then loads and watches MyWPFApp

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
HOST_EXE="$SCRIPT_DIR/WpfHost/bin/Debug/net10.0-windows/win-x64/WpfHost.exe"
APP_DIR="$SCRIPT_DIR/MyWPFApp"

STEAM_ROOT="${HOME}/.local/share/Steam"
PROTON_PATH="${STEAM_ROOT}/steamapps/common/Proton - Experimental"
PROTON_PREFIX="${HOME}/.proton"

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

echo -e "${CYAN}═══════════════════════════════════════════${NC}"
echo -e "${CYAN}  WPF Hot Reload Host Environment${NC}"
echo -e "${CYAN}═══════════════════════════════════════════${NC}"
echo ""

# Build both projects
echo -e "${YELLOW}Building projects...${NC}"
dotnet build "$SCRIPT_DIR/WpfHost/WpfHost.csproj" -v q 2>&1 | tail -3
dotnet build "$SCRIPT_DIR/MyWPFApp/MyWPFApp.csproj" -v q 2>&1 | tail -3

if [ ! -f "$HOST_EXE" ]; then
    echo -e "${YELLOW}Host not found at: $HOST_EXE${NC}"
    exit 1
fi

echo ""
echo -e "${GREEN}Starting WPF Host...${NC}"
echo -e "The host will:"
echo -e "  1. Load MyWPFApp's MainWindow content"
echo -e "  2. Watch for DLL changes"
echo -e "  3. Auto-reload when you rebuild"
echo ""
echo -e "${YELLOW}To trigger hot reload:${NC}"
echo -e "  dotnet build MyWPFApp/MyWPFApp.csproj"
echo ""
echo -e "Press Ctrl+C to stop"
echo ""

STEAM_COMPAT_CLIENT_INSTALL_PATH="$STEAM_ROOT" \
STEAM_COMPAT_DATA_PATH="$PROTON_PREFIX" \
PROTON_USE_WINED3D=1 \
"$PROTON_PATH/proton" run "$HOST_EXE"
