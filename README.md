Para iniciar o projeto em sua máquina, primeiramente você precisa ter o visual studio com Universal Windows instalado. 
Após realizar a instalação do software, você só precisa baixar o projeto e executar a solução em debug ou realease. 
Observação: a primeira vez que você abrir o código ele irá apresentar diversos erros, é normal. Esses erros irão sumir assim que você der um rebuild na solucação. 

Detalhes do código:

1° - Todo design presente no sistema é produzido em cima de um XAML, ou seja, é de fácil conversão para os mais vários tipos de dispositivos.\n 
![image](https://user-images.githubusercontent.com/84163083/193551660-526067bb-bb0a-4071-b923-70c0512d26bb.png)

2° - Utilização no geral. 
Codificação presente no botão "Procurar WiFi".
![image](https://user-images.githubusercontent.com/84163083/193551992-0669ed06-4b06-49b1-8241-10887de0919a.png)
![image](https://user-images.githubusercontent.com/84163083/193552099-1a53126b-1d44-4a1e-bb62-6e686513f8fd.png)
Basicamente o sistema usa o wifi adapter da sua maquina para escanear as redes disponiveis proximas.

Codificação presente no botão "Arquivo".
![image](https://user-images.githubusercontent.com/84163083/193554863-2a97e253-1702-4aa9-af4b-23f9b5983287.png)
![image](https://user-images.githubusercontent.com/84163083/193554994-73ccba13-64bd-49f1-878c-987c1e58fc70.png)
Nesse momento do sistema você seleciona um txt com as senhas que você deseja testar na rede. 

Após todo esse processo você seleciona a rede WiFi que você deseja "invadir" (Lembrando que esse software só deve ser usado afim de estudos), e clica em conectar para que o processo de teste sequencial das senhas inicie. 
![image](https://user-images.githubusercontent.com/84163083/193555824-11afd4b7-ffdd-4ac2-8808-67d2dd37dfda.png)
Utilizamos um foreach para realizar os testes.



