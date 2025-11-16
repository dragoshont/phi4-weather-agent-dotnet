#!/bin/bash
# macOS Setup Script for Phi-4 Weather Agent
# Checks and installs .NET 10 SDK, Foundry Local, and Phi-4 model

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m'

echo -e "${CYAN}=== Phi-4 Weather Agent - macOS Setup ===${NC}"
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

# Check Foundry Local (standalone installation via Homebrew)
echo -e "\n${YELLOW}Checking Foundry Local...${NC}"
if command -v foundry &> /dev/null; then
    FOUNDRY_VERSION=$(foundry --version 2>/dev/null || echo "unknown")
    echo -e "${GREEN}✓ Foundry Local installed: $FOUNDRY_VERSION${NC}"
else
    echo -e "${YELLOW}Installing Foundry Local via Homebrew...${NC}"
    if brew tap microsoft/foundrylocal && brew install foundrylocal; then
        echo -e "${GREEN}✓ Foundry Local installed successfully${NC}"
    else
        echo -e "${RED}✗ Failed to install Foundry Local${NC}"
        echo -e "${YELLOW}  Manual install: brew tap microsoft/foundrylocal && brew install foundrylocal${NC}"
        exit 1
    fi
fi

# Check if Phi-4 model exists
echo -e "\n${YELLOW}Checking Phi-4 model...${NC}"
if foundry cache list 2>/dev/null | grep -q "phi4"; then
    echo -e "${GREEN}✓ Phi-4 model found in cache${NC}"
else
    echo -e "${YELLOW}Downloading Phi-4 model (~7GB, optimized ONNX format)...${NC}"
    echo -e "${CYAN}This may take 5-15 minutes depending on connection speed${NC}"
    echo -e "${GRAY}Note: Using hardware-optimized variant (CPU/GPU/NPU auto-detection)${NC}"
    
    if foundry model download phi4; then
        echo -e "${GREEN}✓ Phi-4 model downloaded successfully${NC}"
    else
        echo -e "${RED}✗ Phi-4 model download failed. Check network connection and disk space.${NC}"
        echo -e "${YELLOW}  Retry: foundry model download phi4${NC}"
        exit 1
    fi
fi

echo -e "\n${CYAN}=== Setup Complete ===${NC}"
echo -e "${GREEN}✓ .NET 10 SDK: $DOTNET_VERSION${NC}"
echo -e "${GREEN}✓ Foundry Local: Installed${NC}"
echo -e "${GREEN}✓ Phi-4 model: Ready${NC}"
echo -e "\n${GREEN}You can now run: dotnet run --project src/Phi4WeatherAgent.AppHost${NC}"
echo -e "${CYAN}Aspire Dashboard will be available at: http://localhost:15888${NC}"
