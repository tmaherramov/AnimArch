# AnimArch AI Virtual Assistant

An AI-powered virtual assistant integrated into the AnimArch software modeling environment. It helps users understand UML diagrams, interact with them using natural language, and work with software models more efficiently.

## Overview

The project extends AnimArch with a virtual assistant based on multimodal large language models (MLLMs). The assistant can communicate with users through a chat interface, understand the currently opened UML diagram, answer questions about its structure, and suggest modifications to the diagram.

The system also includes a visual AI avatar that responds to different interaction states and can be customized using a natural-language description.

## Features

- **AI Chat** with conversation history
- **Visual AI Avatar** with Idle, Waiting, Thinking, and Talking states
- **AI Avatar Customization** using natural-language descriptions
- **UML Diagram Context** automatically provided to the assistant using PlantUML
- **Diagram-aware Questions** about the currently opened UML model
- **AI-assisted UML Modifications** through generated PlantUML and AnimArch suggestions

## UML Integration

When a UML diagram is open, its structure is converted into PlantUML and provided to the virtual assistant as context. This allows the assistant to work with information about classes, attributes, methods, and relationships in the current diagram.

When the user requests a modification, the assistant can generate an updated PlantUML representation. The changes are compared with the current diagram and presented as suggestions through the existing AnimArch suggestion system.

## AI Avatar

The virtual assistant is represented by an animated visual avatar with four main states:

- **Idle** — waiting for interaction
- **Waiting** — processing a user request
- **Thinking** — generating a response
- **Talking** — displaying the assistant's response

The avatar's appearance can also be customized by describing the desired character in natural language. The system generates a consistent set of images for the different avatar states.

## Technologies

- Unity
- Multimodal Large Language Models (MLLMs)
- PlantUML
- TextMeshPro
- AI image generation
- Prompt Engineering

## Research

This project was developed as part of a bachelor's year project at **Comenius University Bratislava**.

The accompanying research focuses on the design and implementation of an AI-powered virtual assistant for software modeling, including its integration with UML diagrams, AI-generated avatar customization, technical challenges, and a proposed evaluation of the system's usefulness and usability.

## Future Work

Potential improvements include:

- voice interaction;
- avatar animations;
- saving customized avatar appearances;
- improved chat management;
- improved handling of diagram modifications;
- better management of multiple suggestions;
- evaluation with real users.

## Author

**Tymur Maherramov**

Comenius University Bratislava  
Faculty of Mathematics, Physics and Informatics
