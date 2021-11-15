using System;
using System.Windows.Forms;

namespace yac8i
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {

            if (args.Length != 1)
            {
                MessageBox.Show("As an argument, please provide path to chip8 program",
                                "Error",
                                MessageBoxButtons.OK, 
                                MessageBoxIcon.Error);
            }
            else
            {
                using (Chip8VM vm = new Chip8VM())
                {

                    if (vm.Load(args[0]))
                    {
                        Application.Run(new MainForm(vm));
                    }
                    else
                    {
                       MessageBox.Show("Unable to initialize chip8 vm.",
                                       "Error",
                                       MessageBoxButtons.OK, 
                                       MessageBoxIcon.Error);
 
                    }
                }

            }

        }


    }
}
