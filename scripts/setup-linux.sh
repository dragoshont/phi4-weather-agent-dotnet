#!/bin/bash
# Linux Setup Script for Phi-4 Weather Agent
# Checks and installs .NET 10 SDK, Aspire workload, Ollama, and Phi-4 model

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

echo -e "${CYAN}=== Phi-4 Weather Agent - Linux Setup ===${NC}"

# Check .NET 10 SDK
echo -e "\n${YELLOW}Checking .NET 10 SDK...${NC}"
DOTNET_VERSION=$(dotnet --version 2>/dev/null)
if [[ $DOTNET_VERSION == 10.* ]]; then
    echo -e "${GREEN}✓ .NET 10 SDK found: $DOTNET_VERSION${NC}"
else
    echo -e "${RED}✗ .NET 10 SDK not found. Please install from https://dotnet.microsoft.com/download/dotnet/10.0${NC}"
    exit 1
fi

# Check Aspire workload
echo -e "\n${YELLOW}Checking Aspire workload...${NC}"
if dotnet workload list | grep -q "aspire"; then
    echo -e "${GREEN}✓ Aspire workload installed${NC}"
else
    echo -e "${YELLOW}Installing Aspire workload...${NC}"
    dotnet workload install aspire
fi

# Note: Linux users should manually install Ollama for local Phi-4 hosting
echo -e "\n${YELLOW}Note: For local AI model hosting on Linux:${NC}"
echo -e "${CYAN}1. Install Ollama: curl -fsSL https://ollama.com/install.sh | sh${NC}"
echo -e "${CYAN}2. Pull Phi-4 model: ollama pull phi4${NC}"
echo -e "${CYAN}3. Verify: ollama list${NC}"
echo -e "\n${YELLOW}These steps are optional and should be done manually.${NC}"

echo -e "\n${CYAN}=== Setup Complete ===${NC}"
echo -e "${GREEN}You can now run: dotnet run --project src/Phi4WeatherAgent.AppHost${NC}"
