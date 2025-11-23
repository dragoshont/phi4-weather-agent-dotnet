#!/bin/bash
# macOS Setup Script for Local AI Agent
# Checks and installs .NET 10 SDK, Foundry Local, and AI models

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m'

echo -e "${CYAN}=== Local AI Agent - macOS Setup ===${NC}"
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

# Check if Phi-4 Mini model exists
echo -e "\n${YELLOW}Checking Phi-4 Mini model...${NC}"
if foundry cache list 2>/dev/null | grep -q "phi4-mini"; then
    echo -e "${GREEN}✓ Phi-4 Mini model found in cache${NC}"
else
    echo -e "${YELLOW}Downloading Phi-4 Mini model (~3.8GB, optimized ONNX format)...${NC}"
    echo -e "${CYAN}This may take 3-8 minutes depending on connection speed${NC}"
    echo -e "${GRAY}Note: Using hardware-optimized variant (CPU/GPU/NPU auto-detection)${NC}"
    echo -e "${GRAY}Please wait... (foundry will show download progress)${NC}"

    # Run with output visible to show progress
    if foundry model download phi4-mini 2>&1; then
        echo -e "${GREEN}✓ Phi-4 Mini model downloaded successfully${NC}"
    else
        echo -e "${RED}✗ Phi-4 Mini model download failed. Check network connection and disk space.${NC}"
        echo -e "${YELLOW}  Retry: foundry model download phi4-mini${NC}"
        exit 1
    fi
fi

# Note: Qwen 2.5 VL 3B requires Ollama (Linux-only in current configuration)
# macOS users can use Phi-4 Mini via Foundry Local
echo -e "\n${CYAN}Note: Qwen 2.5 VL 3B model available via Ollama on Linux${NC}"
echo -e "${GRAY}macOS users: Phi-4 Mini via Foundry Local is recommended${NC}"

# Check if Ollama is installed for Qwen model (alternative to Foundry)
echo -e "\n${YELLOW}Checking Ollama for Qwen model support...${NC}"
if command -v ollama &> /dev/null; then
    echo -e "${GREEN}✓ Ollama found${NC}"

    # Check if Qwen 2.5-VL model exists
    if ollama list 2>/dev/null | grep -q "qwen2.5-vl:3b-instruct"; then
        echo -e "${GREEN}✓ Qwen 2.5-VL 3B model found${NC}"
    else
        echo -e "${YELLOW}Downloading Qwen 2.5-VL 3B model (~2GB)...${NC}"
        echo -e "${CYAN}This may take 2-5 minutes depending on connection speed${NC}"

        if ollama pull qwen2.5-vl:3b-instruct; then
            echo -e "${GREEN}✓ Qwen 2.5-VL model downloaded successfully${NC}"
        else
            echo -e "${YELLOW}⚠ Qwen model download failed (optional - Phi-4 will work)${NC}"
        fi
    fi
else
    echo -e "${CYAN}ℹ Ollama not installed (optional - only needed for Qwen model)${NC}"
    echo -e "${GRAY}  Install: brew install ollama${NC}"
fi

echo -e "\n${CYAN}=== Setup Complete ===${NC}"
echo -e "${GREEN}✓ .NET 10 SDK: $DOTNET_VERSION${NC}"
echo -e "${GREEN}✓ Foundry Local: Installed${NC}"
echo -e "${GREEN}✓ Phi-4 Mini model: Ready${NC}"
if command -v ollama &> /dev/null && ollama list 2>/dev/null | grep -q "qwen2.5-vl:3b-instruct"; then
    echo -e "${GREEN}✓ Qwen 2.5-VL model: Ready${NC}"
fi
echo -e "\n${GREEN}You can now run: dotnet run --project src/LocalAIAgent.AppHost${NC}"
echo -e "${CYAN}Aspire Dashboard will be available at: http://localhost:15888${NC}"
