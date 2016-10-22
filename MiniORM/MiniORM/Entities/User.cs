namespace MiniORM.Entities
{
    using System;
    using Attributes;

    [Entity(TableName = "Users")]
    public class User
    {
        [Id]
        private int id;

        [Column(Name = "Username")]
        private string username;

        [Column(Name = "Password")]
        private string password;

        [Column(Name = "Age")]
        private int age;

        [Column(Name = "RegistrationDate")]
        private DateTime registrationDate;

        public User(string username, string password, int age, DateTime registrationDate)
        {
            this.Username = username;
            this.Password = password;
            this.Age = age;
            this.RegistrationDate = registrationDate;
        }

        public int Id { get; private set; }

        public string Username { get; private set; }

        public string Password { get; private set; }

        public int Age { get; private set; }

        public DateTime RegistrationDate { get; private set; }
    }
}