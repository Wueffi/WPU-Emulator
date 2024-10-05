using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic; // Für Dictionary und Stack
using System.Diagnostics;

public class Emulator : MonoBehaviour
{
    // TMP objects for output and input
    public TMP_InputField cpuCodeInput; // CPU code input
    public TMP_Text regsText; // Display of the 8 registers
    public TMP_Text ramText;  // Display of 16-byte RAM
    public TMP_Text logOutput; // Output log
    public Slider instructionPerSecSlider; // Slider for instructions per second
    public Button runButton; // Button to run the CPU
    public Button stopButton; // Button to stop the CPU
    // CPU internals
    private byte[] registers = new byte[8]; // 8 registers
    private byte[] ram = new byte[16];      // 16 bytes of RAM
    private Stack<int> callStack = new Stack<int>(); // Stack for function calls
    private Dictionary<string, int> labelAddresses = new Dictionary<string, int>();
    private string[] instructions; // Instructions from the input code
    private bool isRunning = false; // To check if the CPU is running
    private int currentInstructionIndex = 0;
    private float instructionDelay = 1.0f; // Delay between instructions
    private float timer = 0f;

    private void Start()
    {
        // Set default values for the slider
        instructionPerSecSlider.minValue = 1; // Minimum 1 instruction per second
        instructionPerSecSlider.maxValue = 2000; // Max 1000 instructions per second
        instructionPerSecSlider.value = 1; // Default value to a safe 10 instructions per second

        // Zero out registers and RAM
        registers[0] = 0; // Hardcoded zero register
        ClearRAM();
        UpdateUI();
    }

    private void Update()
    {
        if (isRunning)
        {
            timer += Time.deltaTime;
            if (timer >= instructionDelay)
            {
                ExecuteNextInstruction();
                timer = 0;
            }
        }
    }

    public void StartCPU()
    {
        Log("CPU Started.");
        ParseInstructions();
        Log($"Total instructions: {instructions.Length}");
        isRunning = true;
        SetInstructionDelay();
        currentInstructionIndex = 0;
        UpdateUI();
    }

    public void StopCPU()
    {
        Log("CPU Stopped.");
        isRunning = false;
    }

    private void ParseInstructions()
    {
        string code = cpuCodeInput.text;
        instructions = code.Split('\n');
        ParseLabels(instructions);  // First pass: Collect labels
    }

    private void ExecuteNextInstruction()
    {
        if (currentInstructionIndex >= instructions.Length)
        {
            Log("Program finished.");
            StopCPU();
            return;
        }

        string instruction = instructions[currentInstructionIndex].Trim();
        if (!string.IsNullOrEmpty(instruction))
        {
            Log($"Decoding: {instruction} at index {currentInstructionIndex}");
            ProcessInstruction(instruction);
        }
        currentInstructionIndex++;
        UpdateUI();
    }

    private void ParseLabels(string[] program)
    {
        labelAddresses.Clear();
        for (int i = 0; i < program.Length; i++)
        {
            string line = program[i].Trim();
            // Check if the line is a label
            if (line.StartsWith(".")) // Wenn du weiterhin Punkte hast, verwende diesen Teil
            {
                string label = line.Substring(1); // Entferne den Punkt
                Log($"Found label: {label}");
                labelAddresses[label] = i; // Speichere die Zeilennummer des Labels
            }
        }

    }

private void ProcessInstruction(string instruction)
{
    string[] parts = instruction.Split(' ');
    if (parts.Length < 2) return;
    int regIndexA, regIndexB, regIndexC, value;

    string command = parts[0].ToUpper();

    Log($"Executing: {instruction}");

    switch (command)
    {
        case "NOOP":
            // No operation, do nothing
            Log("Program finished because of NOOP.");
            StopCPU();
            return;

        case "ADD":
            // ADD REG A, REG B, REG C  -> A = B + C
            regIndexA = GetRegisterIndex(parts[1]); // First operand (A)
            regIndexB = GetRegisterIndex(parts[2]); // Second operand (B)
            regIndexC = GetRegisterIndex(parts[3]); // Third operand (C)
            registers[regIndexC] = (byte)(registers[regIndexA] + registers[regIndexB]);
            break;

        case "SUB":
            // SUB REG A, REG B, REG C -> A = B - C
            regIndexA = GetRegisterIndex(parts[1]);
            regIndexB = GetRegisterIndex(parts[2]);
            regIndexC = GetRegisterIndex(parts[3]);
            registers[regIndexC] = (byte)(registers[regIndexA] - registers[regIndexB]);
            break;

        case "OR":
            // OR REG A, REG B, REG C -> C = A | B
            regIndexA = GetRegisterIndex(parts[1]);
            regIndexB = GetRegisterIndex(parts[2]);
            regIndexC = GetRegisterIndex(parts[3]);
            registers[regIndexC] = (byte)(registers[regIndexA] | registers[regIndexB]);
            break;

        case "XOR":
            // XOR REG A, REG B, REG C -> C = A ^ B
            regIndexA = GetRegisterIndex(parts[1]);
            regIndexB = GetRegisterIndex(parts[2]);
            regIndexC = GetRegisterIndex(parts[3]);
            registers[regIndexC] = (byte)(registers[regIndexA] ^ registers[regIndexB]);
            break;

        case "AND":
            // AND REG A, REG B, REG C -> C = A & B
            regIndexA = GetRegisterIndex(parts[1]);
            regIndexB = GetRegisterIndex(parts[2]);
            regIndexC = GetRegisterIndex(parts[3]);
            registers[regIndexC] = (byte)(registers[regIndexA] & registers[regIndexB]);
            break;

        case "NOT":
            // NOT REG A -> A = ~A (Negate A and store it back into A)
            regIndexA = GetRegisterIndex(parts[1]); // Only one operand, A
            registers[regIndexA] = (byte)~registers[regIndexA];
            break;

        case "RSH":
            // RSH REG A, REG B -> B = A >> 1 (Right-shift A, store result in B)
            regIndexA = GetRegisterIndex(parts[1]);
            regIndexB = GetRegisterIndex(parts[2]);
            registers[regIndexB] = (byte)(registers[regIndexA] >> 1);
            break;

        case "IMM":
            if (parts.Length < 3) // Ensure there are enough parts
            {
                Log($"Error: IMM instruction is malformed: {instruction}");
                return;
            }
            regIndexA = GetRegisterIndex(parts[1]);
            if (regIndexA == -1) return; // Exit if invalid register index

            Log($"Parsing immediate value: {parts[2]}");
            if (int.TryParse(parts[2].Trim(), out value)) // Trim whitespace
            {
                registers[regIndexA] = (byte)value;
            }
            else
            {
                Log($"Error: Invalid immediate value in IMM instruction: {parts[2]}");
            }
            break;

        case "JMP":
            if (parts.Length < 3) 
            {
                string jmpType1 = parts[1]; // 00 for normal jump, 10 for CALL, 01 for RET
                if (jmpType1 == "01")
                {
                    break;
                }
                Log($"Error: JMP instruction is malformed: {instruction}");
                return;
            }
            string jmpType = parts[1]; // 00 for normal jump, 10 for CALL, 01 for RET
            string target = parts[2];
            int address;

            switch (jmpType)
            {
                case "00":
                    // JMP 00 [address/label] -> Normal jump to the given address or label
                    if (labelAddresses.ContainsKey(target))
                    {
                        address = labelAddresses[target]; // It's a label, get the address
                    }
                    else
                    {
                        if (int.TryParse(target, out address)) // Ensure it's a valid address
                        {
                            // Valid address
                        }
                        else
                        {
                            Log($"Error: Invalid address or label in JMP: {target}");
                            return;
                        }
                    }
                    currentInstructionIndex = address; // Set instruction index to the target address
                    break;

                case "10":
                    // CALL
                    if (labelAddresses.ContainsKey(target))
                    {
                        address = labelAddresses[target]; // Resolve label to address
                    }
                    else
                    {
                        if (int.TryParse(target, out address)) // Ensure it's a valid address
                        {
                            // Valid address
                        }
                        else
                        {
                            Log($"Error: Invalid address or label in CALL: {target}");
                            return;
                        }
                    }
                    callStack.Push(currentInstructionIndex + 1); // Save the return address
                    currentInstructionIndex = address; // Jump to the address
                    break;

                case "01":
                    // RET
                    if (callStack.Count > 0)
                    {
                        currentInstructionIndex = callStack.Pop(); // Get return address
                    }
                    else
                    {
                        Log("Error: Call stack is empty, cannot return.");
                        StopCPU();
                    }
                    break;

                default:
                    Log($"Error: Unknown jump type in JMP: {jmpType}");
                    break;
            }
            break;


        case "RLOD":
            // RLOD REG A, RAM B -> Load value from RAM into Register A
            regIndexA = GetRegisterIndex(parts[1]);
            int ramAddress = int.Parse(parts[2]);
            registers[regIndexA] = ram[ramAddress];
            break;

        case "RSTR":
            // RSTR RAM B, REG A -> Store register A into RAM at address B
            ramAddress = int.Parse(parts[2]);
            regIndexA = GetRegisterIndex(parts[1]);
            ram[ramAddress] = registers[regIndexA];
            break;

        default:
            Log($"Unknown or invalid instruction: {instruction}");
            break;
    }
}

    private int GetRegisterIndex(string reg)
    {
        if (int.TryParse(reg, out int regIndex))
        {
            if (regIndex >= 0 && regIndex < registers.Length) // Ensure the register index is valid
            {
                return regIndex;
            }
        }
        Log($"Error: Invalid register index {reg}");
        return -1; // Return -1 if the register index is invalid
    }


    private void ClearRAM()
    {
        for (int i = 0; i < ram.Length; i++)
        {
            ram[i] = 0;
        }
    }

    private void UpdateUI()
    {
        regsText.text = "";
        for (int i = 0; i < registers.Length; i++)
        {
            regsText.text += $"R{i}: {registers[i]} \n";
        }
        ramText.text = "";
        for (int i = 0; i < ram.Length; i++)
        {
            ramText.text += $"Addr {i}: {ram[i]} \n";
        }
    }

    private void Log(string message)
    {
        // Add the new message at the end
        logOutput.text += message + "\n";
        
        // Split the current text into lines
        string[] lines = logOutput.text.Split('\n');
        
        // If there are more than 16 lines, remove the oldest (first) line
        if (lines.Length > 16)
        {
            logOutput.text = string.Join("\n", lines, 1, lines.Length - 1);
        }
    }

    private void SetInstructionDelay()
    {
        // Ensure the slider value is safe and never 0 to avoid division errors
        float sliderValue = Mathf.Max(instructionPerSecSlider.value, 1);
        instructionDelay = 1.0f / sliderValue;
        Log($"Instruction delay set to: {instructionDelay} seconds");
    }
}