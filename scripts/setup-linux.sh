#!/bin/bash
# Linux Setup Script for Phi-4 Weather Agent
# Checks and installs .NET 10 SDK, Ollama, and Phi-4 model

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m'

echo -e "${CYAN}=== Phi-4 Weather Agent - Linux Setup ===${NC}"
echo -e "${GRAY}This script is idempotent - safe to run multiple times${NC}"

# Check .NET 10 SDK
echo -e "\n${YELLOW}Checking .NET 10 SDK...${NC}"
DOTNET_VERSION=$(dotnet --version 2>/dev/null)
if [[ $DOTNET_VERSION == 10.* ]]; then
    echo -e "${GREEN}✓ .NET 10 SDK found: $DOTNET_VERSION${NC}"
else
    echo -e "${RED}✗ .NET 10 SDK not found. Please install from https://dotnet.microsoft.com/download/dotnet/10.0${NC}"
    exit 1
fi

# Note: Aspire workload is no longer needed (Aspire 13.0 uses NuGet packages)

# Check Ollama installation (fully automated)
echo -e "\n${YELLOW}Checking Ollama installation...${NC}"
if command -v ollama &> /dev/null; then
    OLLAMA_VERSION=$(ollama --version 2>/dev/null || echo "unknown")
    echo -e "${GREEN}✓ Ollama installed: $OLLAMA_VERSION${NC}"
else
    echo -e "${YELLOW}Installing Ollama...${NC}"
    if curl -fsSL https://ollama.com/install.sh | sh; then
        echo -e "${GREEN}✓ Ollama installed successfully${NC}"
    else
        echo -e "${RED}✗ Ollama installation failed${NC}"
        echo -e "${YELLOW}  Manual install: curl -fsSL https://ollama.com/install.sh | sh${NC}"
        exit 1
    fi
fi

# Check if Phi-4 model exists
echo -e "\n${YELLOW}Checking Phi-4 model...${NC}"
if ollama list 2>/dev/null | grep -q "phi4"; then
    echo -e "${GREEN}✓ Phi-4 model found${NC}"
else
    echo -e "${YELLOW}Downloading Phi-4 model (~7GB, q4_0 quantized format)...${NC}"
    echo -e "${CYAN}This may take 5-15 minutes depending on connection speed${NC}"
    echo -e "${GRAY}Note: Ollama automatically selects best quantization for your hardware${NC}"
    
    if ollama pull phi4; then
        echo -e "${GREEN}✓ Phi-4 model downloaded successfully${NC}"
    else
        echo -e "${RED}✗ Phi-4 model download failed. Check network connection and disk space.${NC}"
        echo -e "${YELLOW}  Retry: ollama pull phi4${NC}"
        exit 1
    fi
fi

# Verify Ollama service is running
echo -e "\n${YELLOW}Checking Ollama service...${NC}"
if pgrep -x ollama > /dev/null; then
    echo -e "${GREEN}✓ Ollama service is running${NC}"
else
    echo -e "${YELLOW}Starting Ollama service...${NC}"
    # Ollama typically auto-starts after install, but we can trigger it
    ollama list > /dev/null 2>&1 &
    sleep 2
    if pgrep -x ollama > /dev/null; then
        echo -e "${GREEN}✓ Ollama service started${NC}"
    else
        echo -e "${YELLOW}⚠ Ollama service not running. It will auto-start when needed.${NC}"
    fi
fi

echo -e "\n${CYAN}=== Setup Complete ===${NC}"
echo -e "${GREEN}✓ .NET 10 SDK: $DOTNET_VERSION${NC}"
echo -e "${GREEN}✓ Ollama: Installed${NC}"
echo -e "${GREEN}✓ Phi-4 model: Ready${NC}"
echo -e "\n${GREEN}You can now run: dotnet run --project src/Phi4WeatherAgent.AppHost${NC}"
echo -e "${CYAN}Aspire Dashboard will be available at: http://localhost:15888${NC}"
echo -e "${GRAY}Ollama API endpoint: http://localhost:11434${NC}"
