#include <iostream>
#include <fstream>


int main(int argc, char* argv[])
{
    std::ofstream outputFile("test_output.txt");

    if (outputFile.is_open()) {
        outputFile << "Test output start:" << std::endl;

        for (size_t i = 0; i < argc; i++)
        {
            outputFile << argv[i] << std::endl;
        }

        outputFile.close();
        std::cout << "Data written to file successfully!" << std::endl;
    } else {
        std::cerr << "Unable to open file for writing!" << std::endl;
    }

    return 0;
}