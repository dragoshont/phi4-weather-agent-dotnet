#!/bin/bash
# Linux Setup Script for Local AI Agent
# Checks and installs .NET 10 SDK, Ollama, and AI models

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m'

echo -e "${CYAN}=== Local AI Agent - Linux Setup ===${NC}"
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

# Check if Phi-4 model exists (Ollama uses 'phi4' as the standard alias)
echo -e "\n${YELLOW}Checking Phi-4 model...${NC}"
if ollama list 2>/dev/null | grep -q "phi4"; then
    echo -e "${GREEN}✓ Phi-4 model found${NC}"
else
    echo -e "${YELLOW}Downloading Phi-4 model (~7GB, q4_0 quantized format)...${NC}"
    echo -e "${CYAN}This may take 5-15 minutes depending on connection speed${NC}"
    echo -e "${GRAY}Note: Ollama uses 'phi4' as the standard model (comparable to Foundry's phi4-mini)${NC}"
    echo -e "${GRAY}Please wait... (ollama will show download progress)${NC}"

    # Run with output visible to show progress (ollama shows progress by default)
    if ollama pull phi4; then
        echo -e "${GREEN}✓ Phi-4 model downloaded successfully${NC}"
    else
        echo -e "${RED}✗ Phi-4 model download failed. Check network connection and disk space.${NC}"
        echo -e "${YELLOW}  Retry: ollama pull phi4${NC}"
        exit 1
    fi
fi

# Check if Qwen 2.5 VL 3B model exists
echo -e "\n${YELLOW}Checking Qwen 2.5 VL 3B model...${NC}"
if ollama list 2>/dev/null | grep -q "qwen2.5-vl:3b"; then
    echo -e "${GREEN}✓ Qwen 2.5 VL 3B model found${NC}"
else
    echo -e "${YELLOW}Downloading Qwen 2.5 VL 3B model (~2.3GB, quantized format)...${NC}"
    echo -e "${CYAN}This may take 3-10 minutes depending on connection speed${NC}"
    echo -e "${GRAY}Note: Qwen 2.5 VL supports vision and advanced reasoning${NC}"
    echo -e "${GRAY}Please wait... (ollama will show download progress)${NC}"

    # Run with output visible to show progress
    if ollama pull qwen2.5-vl:3b-instruct; then
        echo -e "${GREEN}✓ Qwen 2.5 VL 3B model downloaded successfully${NC}"
    else
        echo -e "${RED}✗ Qwen 2.5 VL 3B model download failed. Check network connection and disk space.${NC}"
        echo -e "${YELLOW}  Retry: ollama pull qwen2.5-vl:3b-instruct${NC}"
        exit 1
    fi
fi

# Check if Qwen 2.5-VL model exists (alternative vision model)
echo -e "\n${YELLOW}Checking Qwen 2.5-VL model...${NC}"
if ollama list 2>/dev/null | grep -q "qwen2.5-vl:3b-instruct"; then
    echo -e "${GREEN}✓ Qwen 2.5-VL 3B model found${NC}"
else
    echo -e "${YELLOW}Downloading Qwen 2.5-VL 3B model (~2GB)...${NC}"
    echo -e "${CYAN}This may take 2-5 minutes depending on connection speed${NC}"
    echo -e "${GRAY}Note: Qwen supports vision capabilities and alternative model selection${NC}"

    if ollama pull qwen2.5-vl:3b-instruct; then
        echo -e "${GREEN}✓ Qwen 2.5-VL model downloaded successfully${NC}"
    else
        echo -e "${YELLOW}⚠ Qwen model download failed (optional - Phi-4 will work)${NC}"
        echo -e "${GRAY}  Retry later: ollama pull qwen2.5-vl:3b-instruct${NC}"
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
if ollama list 2>/dev/null | grep -q "qwen2.5-vl:3b-instruct"; then
    echo -e "${GREEN}✓ Qwen 2.5-VL model: Ready${NC}"
fi
echo -e "\n${GREEN}You can now run: dotnet run --project src/LocalAIAgent.AppHost${NC}"
echo -e "${CYAN}Aspire Dashboard will be available at: http://localhost:15888${NC}"
echo -e "${GRAY}Ollama API endpoint: http://localhost:11434${NC}"
echo -e "${GRAY}Switch models by editing appsettings.json AI:DefaultModel${NC}"
